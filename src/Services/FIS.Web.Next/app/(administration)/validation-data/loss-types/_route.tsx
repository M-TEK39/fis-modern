import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import { hasLegacyRole } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import StatusCardView from "@/components/app-shell/status-card";
import { createLossTypeAction } from "@/app/(administration)/validation-data/loss-types/actions";
import LossTypeForm from "@/app/(administration)/validation-data/loss-types/loss-type-form";
import {
  DEFAULT_LOSS_TYPE_PAGE_SIZE,
  getLossTypesPage,
  LossTypeApiError,
  type LossTypeRecord,
} from "@/lib/api/fleet-operations/api-loss-types";
import { getSession } from "@/lib/auth/session";
import RouteLoading from "@/components/app-shell/route-loading";

export type LossTypeListPageProps = {
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

function editPath(lossTypeCode: number) {
  return `/Validation/MNT_Loss_Type_Edit.aspx?cmbLoss=${encodeURIComponent(String(lossTypeCode))}`;
}

function deleteCheckPath(lossTypeCode: number) {
  return `/Validation/MNT_Loss_Type_Del_Check.aspx?code=${encodeURIComponent(String(lossTypeCode))}`;
}

function ErrorCard({
  message,
  routePath = "/validation-data/loss-types",
}: Readonly<{ message: string; routePath?: string }>) {
  return (
    <StatusCardView
      title="Loss description maintenance"
      message={message}
      retryHref={routePath}
      secondaryHref="/validation-data"
      secondaryLabel="Validation Data"
    />
  );
}

function LossTypeTable({
  lossTypes,
  searchTerm,
  total,
}: Readonly<{ lossTypes: LossTypeRecord[]; searchTerm: string; total: number }>) {
  if (lossTypes.length === 0)
    return (
      <div className="empty-state">
        <h2>No loss descriptions found</h2>
        <p>
          {searchTerm
            ? `No loss descriptions matched “${searchTerm}”.`
            : "Add a loss description using the same legacy validation workflow."}
        </p>
      </div>
    );

  return (
    <div className="table-container">
      <div className="table-header">
        <span className="table-title">
          {total} loss description{total === 1 ? "" : "s"}
        </span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <caption className="sr-only">Loss descriptions</caption>
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>Loss type code</> },
              { key: "column-2", label: <>Loss description</> },
              { key: "column-3", label: <>Last updated</> },
              { key: "column-4", label: <>Actions</> },
            ]}
          />
          <tbody>
            {lossTypes.map((lossType) => (
              <tr key={lossType.lossTypeCode}>
                <td>{lossType.lossTypeCode}</td>
                <td>{valueOrDash(lossType.description)}</td>
                <td>{valueOrDash(lossType.dateUpdated?.slice(0, 10))}</td>
                <td className="actions-column">
                  <div className="table-actions">
                    <Link
                      aria-label={`Edit ${lossType.description || `loss type ${lossType.lossTypeCode}`}`}
                      className="button button-secondary button-small"
                      href={editPath(lossType.lossTypeCode)}
                    >
                      Edit
                    </Link>
                    <Link
                      aria-label={`Delete ${lossType.description || `loss type ${lossType.lossTypeCode}`}`}
                      className="button button-secondary button-small"
                      href={deleteCheckPath(lossType.lossTypeCode)}
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
      <nav className="vehicle-pagination" aria-label="Loss description pages">
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

function LossTypeListView({
  lossTypePage,
  error,
  notice,
  query,
  routePath,
  searchTerm,
}: Readonly<{
  lossTypePage: Awaited<ReturnType<typeof getLossTypesPage>>;
  error: string | undefined;
  notice: string | undefined;
  query: Record<string, string | string[] | undefined>;
  routePath: string;
  searchTerm: string;
}>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="loss-type-list-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Validation / Operational</p>
            <h1 id="loss-type-list-title">Loss Description Maintenance</h1>
            <p>Maintain the loss descriptions used by loss/theft workflows.</p>
          </div>
          <div className="button-row">
            <Link className="button button-secondary" href="/validation-data">
              Validation Data
            </Link>
            <Link className="button button-secondary" href="/home">
              Home
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
            <label htmlFor="loss-type-search">Search loss descriptions</label>
            <input
              id="loss-type-search"
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
        <LossTypeTable
          lossTypes={lossTypePage.items}
          searchTerm={searchTerm}
          total={lossTypePage.total}
        />
        <Pagination
          page={lossTypePage.page}
          pageSize={lossTypePage.pageSize}
          query={query}
          routePath={routePath}
          total={lossTypePage.total}
          totalPages={lossTypePage.totalPages}
        />
        <LossTypeForm
          action={createLossTypeAction}
          lossType={{
            lossTypeCode: 0,
            description: "",
            dateCreated: null,
            dateUpdated: null,
            createdByUserCode: null,
            modifiedByUserCode: null,
            isDeleted: false,
          }}
          mode="create"
        />
        <div className="vehicle-footer-actions">
          <form action={logoutAction}>
            <button className="button button-secondary" type="submit">
              Sign out
            </button>
          </form>
        </div>
      </section>
    </main>
  );
}

const LossTypeListPageContent = renderLossTypeListPageContent;

async function renderLossTypeListPageContent({
  searchParams,
  routePath = "/validation-data/loss-types",
}: LossTypeListPageProps) {
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
          message="Loss description maintenance is temporarily unavailable."
          routePath={routePath}
        />
      </main>
    );
  if (!hasLegacyRole(session.roles, "Validation"))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard
          message="You do not have permission to maintain loss descriptions."
          routePath="/home"
        />
      </main>
    );

  const query = await searchParams;
  const saved = getQueryValue(query.saved);
  const error = getQueryValue(query.error);
  const searchTerm = (getQueryValue(query.searchTerm) ?? "").trim();
  const requestedPage = positivePage(getQueryValue(query.page));
  try {
    const lossTypePage = await getLossTypesPage({
      page: requestedPage,
      pageSize: DEFAULT_LOSS_TYPE_PAGE_SIZE,
      searchTerm,
    });
    const notice =
      saved === "created"
        ? "Loss description added successfully."
        : saved === "updated"
          ? "Loss description updated successfully."
          : saved === "deleted"
            ? "Loss description deleted successfully."
            : error;
    return (
      <LossTypeListView
        lossTypePage={lossTypePage}
        error={error}
        notice={notice}
        query={query}
        routePath={routePath}
        searchTerm={searchTerm}
      />
    );
  } catch (caughtError) {
    if (caughtError instanceof LossTypeApiError && caughtError.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS loss type list request failed",
      caughtError instanceof Error ? caughtError.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Loss descriptions could not be loaded." routePath={routePath} />
      </main>
    );
  }
}

export function LossTypeListPageRoute(props: LossTypeListPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LossTypeListPageContent {...props} />
    </Suspense>
  );
}

export default function LossTypeListPage(props: Pick<LossTypeListPageProps, "searchParams">) {
  return <LossTypeListPageRoute {...props} />;
}
