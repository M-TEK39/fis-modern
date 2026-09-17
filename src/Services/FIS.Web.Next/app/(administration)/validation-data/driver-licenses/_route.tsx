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
  DEFAULT_DRIVER_LICENCE_PAGE_SIZE,
  DriverLicenceApiError,
  getDriverLicencesPage,
  type DriverLicenceRecord,
} from "@/lib/api/reference-data/api-driver-licences";
import { getSession } from "@/lib/auth/session";
import RouteLoading from "@/components/app-shell/route-loading";

export type DriverLicenceListPageProps = {
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

function editPath(licenceCode: number) {
  return `/Validation/MNT_DriversLicence_Edit.aspx?cmbDriverL=${encodeURIComponent(String(licenceCode))}`;
}

function deleteCheckPath(licenceCode: number) {
  return `/Validation/MNT_DriversLicence_Del_Check.aspx?code=${encodeURIComponent(String(licenceCode))}`;
}

function ErrorCard({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Driver licence maintenance</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/validation-data">
        Validation Data
      </Link>
    </section>
  );
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return (
    <ApiUnavailableCard
      message="Driver licences could not be loaded."
      retryHref={routePath}
      secondaryHref="/validation-data"
      secondaryLabel="Validation Data"
      showIcon={false}
    />
  );
}

function DriverLicenceTable({
  licences,
  searchTerm,
  total,
}: Readonly<{ licences: DriverLicenceRecord[]; searchTerm: string; total: number }>) {
  if (licences.length === 0)
    return (
      <div className="empty-state">
        <h2>No driver licence types found</h2>
        <p>
          {searchTerm
            ? `No driver licence types matched “${searchTerm}”.`
            : "Add a driver licence type using the same legacy validation workflow."}
        </p>
        <Link className="button button-primary" href="/Validation/MNT_Driverslicence_Add.aspx">
          Add Driver Licence
        </Link>
      </div>
    );

  return (
    <div className="table-container">
      <div className="table-header">
        <span className="table-title">
          {total} driver licence{total === 1 ? "" : "s"}
        </span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <caption className="sr-only">Legacy driver licence types</caption>
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>Licence code</> },
              { key: "column-2", label: <>Description</> },
              { key: "column-3", label: <>Last updated</> },
              { key: "column-4", label: <>Actions</> },
            ]}
          />
          <tbody>
            {licences.map((licence) => (
              <tr key={licence.licenceCode}>
                <td>{licence.licenceCode}</td>
                <td>{valueOrDash(licence.description)}</td>
                <td>{valueOrDash(licence.dateUpdated?.slice(0, 10))}</td>
                <td className="actions-column">
                  <div className="table-actions">
                    <Link
                      aria-label={`Edit ${licence.description || `driver licence ${licence.licenceCode}`}`}
                      className="button button-secondary button-small"
                      href={editPath(licence.licenceCode)}
                    >
                      Edit
                    </Link>
                    <Link
                      aria-label={`Delete ${licence.description || `driver licence ${licence.licenceCode}`}`}
                      className="button button-secondary button-small"
                      href={deleteCheckPath(licence.licenceCode)}
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
      <nav className="vehicle-pagination" aria-label="Driver licence pages">
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

function DriverLicenceListView({
  licencePage,
  error,
  notice,
  query,
  routePath,
  searchTerm,
}: Readonly<{
  licencePage: Awaited<ReturnType<typeof getDriverLicencesPage>>;
  error: string | undefined;
  notice: string | undefined;
  query: Record<string, string | string[] | undefined>;
  routePath: string;
  searchTerm: string;
}>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="driver-licence-list-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Validation / Licence</p>
            <h1 id="driver-licence-list-title">Drivers Licence Maintenance</h1>
            <p>Maintain the legacy driver licence descriptions used by vehicle models.</p>
          </div>
          <div className="button-row">
            <Link className="button button-primary" href="/Validation/MNT_Driverslicence_Add.aspx">
              Add Driver Licence
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
            <label htmlFor="driver-licence-search">Search driver licence descriptions</label>
            <input
              id="driver-licence-search"
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
        <DriverLicenceTable
          licences={licencePage.items}
          searchTerm={searchTerm}
          total={licencePage.total}
        />
        <Pagination
          page={licencePage.page}
          pageSize={licencePage.pageSize}
          query={query}
          routePath={routePath}
          total={licencePage.total}
          totalPages={licencePage.totalPages}
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

const DriverLicenceListPageContent = renderDriverLicenceListPageContent;

async function renderDriverLicenceListPageContent({
  searchParams,
  routePath = "/validation-data/driver-licenses",
}: DriverLicenceListPageProps) {
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
        <ErrorCard message="You do not have permission to maintain driver licences." />
      </main>
    );

  const query = await searchParams;
  const saved = getQueryValue(query.saved);
  const error = getQueryValue(query.error);
  const searchTerm = (getQueryValue(query.searchTerm) ?? "").trim();
  const requestedPage = positivePage(getQueryValue(query.page));
  try {
    const licencePage = await getDriverLicencesPage({
      page: requestedPage,
      pageSize: DEFAULT_DRIVER_LICENCE_PAGE_SIZE,
      searchTerm,
    });
    const notice =
      saved === "created"
        ? "Driver licence added successfully."
        : saved === "updated"
          ? "Driver licence updated successfully."
          : saved === "deleted"
            ? "Driver licence deleted successfully."
            : error;
    return (
      <DriverLicenceListView
        licencePage={licencePage}
        error={error}
        notice={notice}
        query={query}
        routePath={routePath}
        searchTerm={searchTerm}
      />
    );
  } catch (caughtError) {
    if (caughtError instanceof DriverLicenceApiError && caughtError.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS driver licence list request failed",
      caughtError instanceof Error ? caughtError.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  }
}

export function DriverLicenceListPageRoute(props: DriverLicenceListPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <DriverLicenceListPageContent {...props} />
    </Suspense>
  );
}

export default function DriverLicenceListPage(
  props: Pick<DriverLicenceListPageProps, "searchParams">,
) {
  return <DriverLicenceListPageRoute {...props} />;
}
