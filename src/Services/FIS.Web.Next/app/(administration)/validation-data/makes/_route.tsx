import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import { hasLegacyRole } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import ApiUnavailableCard from "@/components/app-shell/api-unavailable-card";
import {
  DEFAULT_MAKE_PAGE_SIZE,
  getMakesPage,
  MakeApiError,
  type MakePage,
  type MakeRecord,
} from "@/lib/api/reference-data/api-makes";
import { getSession } from "@/lib/auth/session";
import RouteLoading from "@/components/app-shell/route-loading";

export type MakeListPageProps = {
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
    for (const item of Array.isArray(value) ? value : [value]) {
      if (item) params.append(key, item);
    }
  }
  if (page > 1) params.set("page", String(page));
  const queryString = params.toString();
  return queryString ? `${routePath}?${queryString}` : routePath;
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function editPath(makeCode: number) {
  return `/Validation/MNT_Make_Edit.aspx?cmbMake=${encodeURIComponent(String(makeCode))}`;
}

function deleteCheckPath(makeCode: number) {
  return `/Validation/MNT_Make_Del_Check.aspx?code=${encodeURIComponent(String(makeCode))}`;
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to maintain vehicle makes.</h2>
      <Link className="button button-secondary" href="/validation-data">
        Validation Data
      </Link>
    </section>
  );
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return (
    <ApiUnavailableCard
      message="Vehicle makes could not be loaded."
      retryHref={routePath}
      secondaryHref="/validation-data"
      secondaryLabel="Validation Data"
      showIcon={false}
    />
  );
}

function MakeTable({ makes, total }: Readonly<{ makes: MakeRecord[]; total: number }>) {
  if (makes.length === 0)
    return (
      <div className="empty-state">
        <h2>No makes found</h2>
        <p>Add a make using the legacy vehicle-validation workflow.</p>
        <Link className="button button-primary" href="/Validation/MNT_Make_Add.aspx">
          Add Make
        </Link>
      </div>
    );
  return (
    <div className="table-container">
      <div className="table-header">
        <span className="table-title">
          {total} make{total === 1 ? "" : "s"}
        </span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <caption className="sr-only">Legacy vehicle makes</caption>
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>Make code</> },
              { key: "column-2", label: <>Make name</> },
              { key: "column-3", label: <>Last updated</> },
              { key: "column-4", label: <>Actions</> },
            ]}
          />
          <tbody>
            {makes.map((make) => (
              <tr key={make.makeCode}>
                <td>{make.makeCode}</td>
                <td>{valueOrDash(make.makeDescription)}</td>
                <td>{valueOrDash(make.dateUpdated?.slice(0, 10))}</td>
                <td className="actions-column">
                  <div className="table-actions">
                    <Link
                      className="button button-secondary button-small"
                      href={editPath(make.makeCode)}
                    >
                      Edit
                    </Link>
                    <Link
                      className="button button-secondary button-small"
                      href={deleteCheckPath(make.makeCode)}
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

function MakePagination({
  routePath,
  query,
  page,
  totalPages,
}: Readonly<{
  routePath: string;
  query: Record<string, string | string[] | undefined>;
  page: number;
  totalPages: number;
}>) {
  if (totalPages <= 1) return null;

  return (
    <nav className="vehicle-pagination" aria-label="Make pages">
      {page > 1 ? (
        <Link
          className="vehicle-pagination-button"
          href={pageHref(routePath, query, page - 1)}
          aria-label="Go to previous make page"
        >
          Previous
        </Link>
      ) : (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Previous
        </span>
      )}
      <span className="vehicle-pagination-meta" aria-live="polite">
        Page {page} of {totalPages}
      </span>
      {page < totalPages ? (
        <Link
          className="vehicle-pagination-button"
          href={pageHref(routePath, query, page + 1)}
          aria-label="Go to next make page"
        >
          Next
        </Link>
      ) : (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Next
        </span>
      )}
    </nav>
  );
}

function MakeListView({
  makePage,
  error,
  notice,
  query,
  routePath,
}: Readonly<{
  makePage: MakePage;
  error: string | undefined;
  notice: string | undefined;
  query: Record<string, string | string[] | undefined>;
  routePath: string;
}>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="make-list-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Validation / Vehicle</p>
            <h1 id="make-list-title">Make Maintenance</h1>
            <p>Maintain the legacy vehicle make list used by model and vehicle workflows.</p>
          </div>
          <div className="button-row">
            <Link className="button button-primary" href="/Validation/MNT_Make_Add.aspx">
              Add Make
            </Link>
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
        <MakeTable makes={makePage.items} total={makePage.total} />
        <MakePagination
          routePath={routePath}
          query={query}
          page={makePage.page}
          totalPages={makePage.totalPages}
        />
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
}

const MakeListPageContent = renderMakeListPageContent;

async function renderMakeListPageContent({
  searchParams,
  routePath = "/validation-data/makes",
}: MakeListPageProps) {
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
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  if (!hasLegacyRole(session.roles, "Validation"))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const saved = getQueryValue(query.saved);
  const error = getQueryValue(query.error);
  const requestedPage = positivePage(getQueryValue(query.page));
  try {
    const makePage: MakePage = await getMakesPage({
      page: requestedPage,
      pageSize: DEFAULT_MAKE_PAGE_SIZE,
    });
    const notice =
      saved === "created"
        ? "Make added successfully."
        : saved === "updated"
          ? "Make updated successfully."
          : saved === "deleted"
            ? "Make deleted successfully."
            : error;
    return (
      <MakeListView
        makePage={makePage}
        error={error}
        notice={notice}
        query={query}
        routePath={routePath}
      />
    );
  } catch (error) {
    if (error instanceof MakeApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS make list request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  }
}

export function MakeListPageRoute(props: MakeListPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <MakeListPageContent {...props} />
    </Suspense>
  );
}

export default function MakeListPage(props: Pick<MakeListPageProps, "searchParams">) {
  return <MakeListPageRoute {...props} />;
}
