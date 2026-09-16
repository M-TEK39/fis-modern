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
  DEFAULT_SITE_PAGE_SIZE,
  getSitesPage,
  SiteApiError,
  type SiteRecord,
} from "@/lib/api/reference-data/api-sites";
import { getSession } from "@/lib/auth/session";
import RouteLoading from "@/components/app-shell/route-loading";

export type SiteListPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function queryValue(value: string | string[] | undefined) {
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
function editPath(siteCode: number) {
  return `/Validation/MNT_Site_Edit.aspx?cmbSite=${encodeURIComponent(String(siteCode))}`;
}
function deletePath(siteCode: number) {
  return `/Validation/MNT_Site_Del_Check.aspx?code=${encodeURIComponent(String(siteCode))}`;
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to maintain sites.</h2>
      <Link className="button button-secondary" href="/validation-data">
        Validation Data
      </Link>
    </section>
  );
}
function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return (
    <ApiUnavailableCard
      message="Site data could not be loaded."
      retryHref={routePath}
      secondaryHref="/validation-data"
      secondaryLabel="Validation Data"
      showIcon={false}
    />
  );
}

function SiteTable({ sites, total }: Readonly<{ sites: SiteRecord[]; total: number }>) {
  if (sites.length === 0)
    return (
      <div className="empty-state">
        <h2>No active sites found</h2>
        <p>Add a site using the same legacy organisation-maintenance workflow.</p>
        <Link className="button button-primary" href="/Validation/MNT_SiteAdd.aspx">
          Add Site
        </Link>
      </div>
    );
  return (
    <div className="table-container">
      <div className="table-header">
        <span className="table-title">
          {total} site{total === 1 ? "" : "s"}
        </span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <caption className="sr-only">Active legacy site records</caption>
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>Site number</> },
              { key: "column-2", label: <>Description</> },
              { key: "column-3", label: <>Responsible person</> },
              { key: "column-4", label: <>Telephone</> },
              { key: "column-5", label: <>Status</> },
              { key: "column-6", label: <>Actions</> },
            ]}
          />
          <tbody>
            {sites.map((site) => (
              <tr key={site.siteCode}>
                <td>{valueOrDash(site.departmentNumber)}</td>
                <td>{valueOrDash(site.description)}</td>
                <td>{valueOrDash(site.responsiblePerson)}</td>
                <td>{valueOrDash(site.telephone)}</td>
                <td>
                  <span className={`badge ${site.siteActive ? "badge-success" : "badge-warning"}`}>
                    {site.siteActive ? "Active" : "Inactive"}
                  </span>
                </td>
                <td className="actions-column">
                  <div className="table-actions">
                    <Link
                      className="button button-secondary button-small"
                      href={editPath(site.siteCode)}
                    >
                      Edit
                    </Link>
                    <Link
                      className="button button-secondary button-small"
                      href={deletePath(site.siteCode)}
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

function SitePagination({
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
    <nav className="vehicle-pagination" aria-label="Site pages">
      {page > 1 ? (
        <Link
          className="vehicle-pagination-button"
          href={pageHref(routePath, query, page - 1)}
          aria-label="Go to previous site page"
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
          aria-label="Go to next site page"
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

function SiteListView({
  sitePage,
  error,
  notice,
  query,
  routePath,
}: Readonly<{
  sitePage: Awaited<ReturnType<typeof getSitesPage>>;
  error: string | undefined;
  notice: string | undefined;
  query: Record<string, string | string[] | undefined>;
  routePath: string;
}>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="site-list-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Validation / Organisation</p>
            <h1 id="site-list-title">Site Maintenance</h1>
            <p>Maintain active and inactive site records without dropping legacy fields.</p>
          </div>
          <div className="button-row">
            <Link className="button button-primary" href="/Validation/MNT_SiteAdd.aspx">
              Add Site
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
        <SiteTable sites={sitePage.items} total={sitePage.total} />
        <SitePagination
          routePath={routePath}
          query={query}
          page={sitePage.page}
          totalPages={sitePage.totalPages}
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

const SiteListPageContent = renderSiteListPageContent;

async function renderSiteListPageContent({
  searchParams,
  routePath = "/validation-data/sites",
}: SiteListPageProps) {
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
  const saved = queryValue(query.saved);
  const error = queryValue(query.error);
  const requestedPage = positivePage(queryValue(query.page));
  try {
    const sitePage = await getSitesPage({
      page: requestedPage,
      pageSize: DEFAULT_SITE_PAGE_SIZE,
    });
    const notice =
      saved === "created"
        ? "Site added successfully."
        : saved === "updated"
          ? "Site updated successfully."
          : saved === "deleted"
            ? "Site deleted successfully."
            : error;
    return (
      <SiteListView
        sitePage={sitePage}
        error={error}
        notice={notice}
        query={query}
        routePath={routePath}
      />
    );
  } catch (error) {
    if (error instanceof SiteApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS site list request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  }
}

export function SiteListPageRoute(props: SiteListPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <SiteListPageContent {...props} />
    </Suspense>
  );
}

export default function SiteListPage(props: Pick<SiteListPageProps, "searchParams">) {
  return <SiteListPageRoute {...props} />;
}
