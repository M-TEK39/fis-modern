import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { createExtraCodeAction } from "@/app/(administration)/validation-data/extras/actions";
import ExtraCodeForm from "@/app/(administration)/validation-data/extras/extra-code-form";
import {
  DEFAULT_EXTRA_CODE_PAGE_SIZE,
  ExtraCodeApiError,
  getExtraCodesPage,
  type ExtraCodeRecord,
} from "@/lib/api/reference-data/api-extra-codes";
import { getSession } from "@/lib/auth/session";
import RouteLoading from "@/components/app-shell/route-loading";

export type ExtraCodeListPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positivePage(value: string | undefined) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : 1;
}

function pageHref(
  routePath: string,
  query: Record<string, string | string[] | undefined>,
  page: number,
) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (key === "page" || value === undefined) continue;
    for (const item of Array.isArray(value) ? value : [value]) params.append(key, item);
  }
  if (page > 1) params.set("page", String(page));
  const queryString = params.toString();
  return queryString ? `${routePath}?${queryString}` : routePath;
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function ErrorCard({
  message,
  routePath = "/validation-data/extras",
}: Readonly<{ message: string; routePath?: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>{message}</h2>
      <div className="button-row">
        <Link className="button button-primary" href={routePath}>
          Try again
        </Link>
        <Link className="button button-secondary" href="/validation-data">
          Validation Data
        </Link>
      </div>
    </section>
  );
}

function ExtraCodeTable({
  extras,
  searchTerm,
  total,
}: Readonly<{ extras: ExtraCodeRecord[]; searchTerm: string; total: number }>) {
  if (extras.length === 0) {
    return (
      <div className="empty-state">
        <h2>No extras found</h2>
        <p>
          {searchTerm
            ? `No extras matched “${searchTerm}”.`
            : "Add an extra using the same legacy validation workflow."}
        </p>
      </div>
    );
  }

  return (
    <div className="table-container">
      <div className="table-header">
        <span className="table-title">
          {total} extra{total === 1 ? "" : "s"}
        </span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <caption className="sr-only">Optional extras</caption>
          <thead>
            <tr>
              <th scope="col">Extra code</th>
              <th scope="col">Extra description</th>
              <th scope="col">Actions</th>
            </tr>
          </thead>
          <tbody>
            {extras.map((extra) => (
              <tr key={extra.extraCode}>
                <td>{extra.extraCode}</td>
                <td>{valueOrDash(extra.description)}</td>
                <td className="actions-column">
                  <div className="table-actions">
                    <Link
                      className="button button-secondary button-small"
                      href={`/validation-data/extras/delete?code=${encodeURIComponent(String(extra.extraCode))}`}
                    >
                      Delete
                    </Link>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function Pagination({
  page,
  pageSize,
  query,
  routePath,
  total,
  totalPages,
}: Readonly<{
  page: number;
  pageSize: number;
  query: Record<string, string | string[] | undefined>;
  routePath: string;
  total: number;
  totalPages: number;
}>) {
  if (totalPages <= 1) return null;

  return (
    <>
      <nav className="vehicle-pagination" aria-label="Extra code pages">
        {page <= 1 ? (
          <span
            className="vehicle-pagination-button vehicle-pagination-disabled"
            aria-disabled="true"
          >
            Previous
          </span>
        ) : (
          <Link className="vehicle-pagination-button" href={pageHref(routePath, query, page - 1)}>
            Previous
          </Link>
        )}
        <span className="vehicle-pagination-meta" aria-live="polite">
          Page {page} of {totalPages}
        </span>
        {page >= totalPages ? (
          <span
            className="vehicle-pagination-button vehicle-pagination-disabled"
            aria-disabled="true"
          >
            Next
          </span>
        ) : (
          <Link className="vehicle-pagination-button" href={pageHref(routePath, query, page + 1)}>
            Next
          </Link>
        )}
      </nav>
      <p className="vehicle-pagination-meta">
        Total records: {total} | Page size: {pageSize}
      </p>
    </>
  );
}

async function ExtraCodeListPageContent({
  searchParams,
  routePath = "/validation-data/extras",
}: ExtraCodeListPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard
          message="Extra code maintenance is temporarily unavailable."
          routePath={routePath}
        />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to maintain extras." routePath="/home" />
      </main>
    );

  const query = await searchParams;
  const saved = getQueryValue(query.saved);
  const error = getQueryValue(query.error);
  const searchTerm = (getQueryValue(query.searchTerm) ?? "").trim();
  const requestedPage = positivePage(getQueryValue(query.page));
  try {
    const extraPage = await getExtraCodesPage({
      page: requestedPage,
      pageSize: DEFAULT_EXTRA_CODE_PAGE_SIZE,
      searchTerm,
    });
    const notice =
      saved === "created"
        ? "Extra added successfully."
        : saved === "deleted"
          ? "Extra deleted successfully."
          : error;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="extra-code-list-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Vehicle</p>
              <h1 id="extra-code-list-title">Optional Extras Maintenance</h1>
              <p>Maintain the optional extras used by vehicle data and job-card workflows.</p>
            </div>
            <div className="button-row">
              <Link className="button button-secondary" href="/validation-data">
                Validation Data
              </Link>
            </div>
          </header>
          {notice ? (
            <div
              className={`notice ${error ? "notice-error" : "notice-success"}`}
              role={error ? "alert" : "status"}
            >
              {notice}
            </div>
          ) : null}
          <form className="vehicle-quick-search-form" method="get" action={routePath}>
            <div className="field">
              <label htmlFor="extra-code-search">Search extra descriptions</label>
              <input
                id="extra-code-search"
                name="searchTerm"
                type="search"
                defaultValue={searchTerm}
                placeholder="Enter a description"
              />
            </div>
            <div className="button-row vehicle-quick-search-actions">
              <button className="button button-primary" type="submit">
                Search
              </button>
              {searchTerm ? (
                <Link className="button button-secondary" href={routePath}>
                  Clear
                </Link>
              ) : null}
            </div>
          </form>
          <ExtraCodeTable
            extras={extraPage.items}
            searchTerm={searchTerm}
            total={extraPage.total}
          />
          <Pagination
            page={extraPage.page}
            pageSize={extraPage.pageSize}
            query={query}
            routePath={routePath}
            total={extraPage.total}
            totalPages={extraPage.totalPages}
          />
          <ExtraCodeForm action={createExtraCodeAction} />
          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
            <form action={logoutAction}>
              <button className="button button-secondary" type="submit">
                Sign out
              </button>
            </form>
          </div>
        </section>
      </main>
    );
  } catch (caughtError) {
    if (caughtError instanceof ExtraCodeApiError && caughtError.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS extra code list request failed",
      caughtError instanceof Error ? caughtError.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Extra code data could not be loaded." routePath={routePath} />
      </main>
    );
  }
}

export function ExtraCodeListPageRoute(props: ExtraCodeListPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <ExtraCodeListPageContent {...props} />
    </Suspense>
  );
}

export default function ExtraCodeListPage(props: Pick<ExtraCodeListPageProps, "searchParams">) {
  return <ExtraCodeListPageRoute {...props} />;
}
