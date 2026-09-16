import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import { hasLegacyRole } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  DEFAULT_LICENSE_FEE_PAGE_SIZE,
  LicenseFeeApiError,
  getLicenseFeesPage,
  type LicenseFeeRecord,
} from "@/lib/api/reference-data/api-license-fees";
import { getSession } from "@/lib/auth/session";
import RouteLoading from "@/components/app-shell/route-loading";

export type LicenseFeeListPageProps = {
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

function editPath(code: number) {
  return `/Validation/MNT_Licence_Fee_Edit.aspx?cmbLicence=${encodeURIComponent(String(code))}`;
}

function deleteCheckPath(code: number) {
  return `/Validation/MNT_Licence_Fee_Del_Check.aspx?code=${encodeURIComponent(String(code))}`;
}

function ErrorCard({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/validation-data">
        Validation Data
      </Link>
    </section>
  );
}

function LicenseFeeTable({
  fees,
  searchTerm,
  total,
}: Readonly<{ fees: LicenseFeeRecord[]; searchTerm: string; total: number }>) {
  if (fees.length === 0)
    return (
      <div className="empty-state">
        <h2>No licence fees found</h2>
        <p>
          {searchTerm
            ? `No licence fees matched “${searchTerm}”.`
            : "Add a licence fee using the same legacy validation workflow."}
        </p>
        <Link className="button button-primary" href="/Validation/MNT_Licence_Fee_Add.aspx">
          Add Licence Fee
        </Link>
      </div>
    );
  return (
    <div className="table-container">
      <div className="table-header">
        <span className="table-title">
          {total} licence fee{total === 1 ? "" : "s"}
        </span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <caption className="sr-only">Legacy licence fees</caption>
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>Licence fee code</> },
              { key: "column-2", label: <>Description</> },
              { key: "column-3", label: <>Yearly tariff</> },
              { key: "column-4", label: <>Actions</> },
            ]}
          />
          <tbody>
            {fees.map((fee) => (
              <tr key={fee.licenceFeeCode}>
                <td>{fee.licenceFeeCode}</td>
                <td>{valueOrDash(fee.description)}</td>
                <td>{fee.fee === null ? "-" : fee.fee.toFixed(2)}</td>
                <td className="actions-column">
                  <div className="table-actions">
                    <Link
                      className="button button-secondary button-small"
                      href={editPath(fee.licenceFeeCode)}
                    >
                      Edit
                    </Link>
                    <Link
                      className="button button-secondary button-small"
                      href={deleteCheckPath(fee.licenceFeeCode)}
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
      <nav className="vehicle-pagination" aria-label="Licence fee pages">
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

function LicenseFeeListView({
  feePage,
  error,
  notice,
  query,
  routePath,
  searchTerm,
}: Readonly<{
  feePage: Awaited<ReturnType<typeof getLicenseFeesPage>>;
  error: string | undefined;
  notice: string | undefined;
  query: Record<string, string | string[] | undefined>;
  routePath: string;
  searchTerm: string;
}>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="license-fee-list-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Validation / Licence</p>
            <h1 id="license-fee-list-title">License Fees Maintenance</h1>
            <p>Maintain the legacy licence fee records used by vehicle models.</p>
          </div>
          <div className="button-row">
            <Link className="button button-primary" href="/Validation/MNT_Licence_Fee_Add.aspx">
              Add Licence Fee
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
        <form className="vehicle-quick-search-form" method="get" action={routePath}>
          <div className="field">
            <label htmlFor="license-fee-search">Search licence fee descriptions</label>
            <input
              id="license-fee-search"
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
        <LicenseFeeTable fees={feePage.items} searchTerm={searchTerm} total={feePage.total} />
        <Pagination
          page={feePage.page}
          pageSize={feePage.pageSize}
          query={query}
          routePath={routePath}
          total={feePage.total}
          totalPages={feePage.totalPages}
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

const LicenseFeeListPageContent = renderLicenseFeeListPageContent;

async function renderLicenseFeeListPageContent({
  searchParams,
  routePath = "/validation-data/license-fees",
}: LicenseFeeListPageProps) {
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
        <ErrorCard message="Licence fee maintenance is temporarily unavailable." />
      </main>
    );
  if (!hasLegacyRole(session.roles, "Validation"))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to maintain licence fees." />
      </main>
    );

  const query = await searchParams;
  const saved = getQueryValue(query.saved);
  const error = getQueryValue(query.error);
  const searchTerm = (getQueryValue(query.searchTerm) ?? "").trim();
  const requestedPage = positivePage(getQueryValue(query.page));
  try {
    const feePage = await getLicenseFeesPage({
      page: requestedPage,
      pageSize: DEFAULT_LICENSE_FEE_PAGE_SIZE,
      searchTerm,
    });
    const notice =
      saved === "created"
        ? "Licence fee added successfully."
        : saved === "updated"
          ? "Licence fee updated successfully."
          : saved === "deleted"
            ? "Licence fee deleted successfully."
            : error;
    return (
      <LicenseFeeListView
        feePage={feePage}
        error={error}
        notice={notice}
        query={query}
        routePath={routePath}
        searchTerm={searchTerm}
      />
    );
  } catch (caughtError) {
    if (caughtError instanceof LicenseFeeApiError && caughtError.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS licence fee list request failed",
      caughtError instanceof Error ? caughtError.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Licence fee data could not be loaded." />
      </main>
    );
  }
}

export function LicenseFeeListPageRoute(props: LicenseFeeListPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LicenseFeeListPageContent {...props} />
    </Suspense>
  );
}

export default function LicenseFeeListPage(props: Pick<LicenseFeeListPageProps, "searchParams">) {
  return <LicenseFeeListPageRoute {...props} />;
}
