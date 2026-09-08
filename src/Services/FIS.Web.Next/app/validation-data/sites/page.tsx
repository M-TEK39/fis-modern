import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { logoutAction } from "@/app/actions/auth";
import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { SiteApiError, getSites, type SiteRecord } from "@/lib/api-sites";
import { getSession } from "@/lib/session";

export type SiteListPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
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
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Site data could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
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

function SiteTable({ sites }: Readonly<{ sites: SiteRecord[] }>) {
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
          {sites.length} site{sites.length === 1 ? "" : "s"}
        </span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <caption className="sr-only">Active legacy site records</caption>
          <thead>
            <tr>
              <th scope="col">Site number</th>
              <th scope="col">Description</th>
              <th scope="col">Responsible person</th>
              <th scope="col">Telephone</th>
              <th scope="col">Status</th>
              <th scope="col">Actions</th>
            </tr>
          </thead>
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

export default async function SiteListPage({
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
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const saved = queryValue(query.saved);
  const error = queryValue(query.error);
  try {
    const sites = await getSites();
    const notice =
      saved === "created"
        ? "Site added successfully."
        : saved === "updated"
          ? "Site updated successfully."
          : saved === "deleted"
            ? "Site deleted successfully."
            : error;
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
          <SiteTable sites={sites} />
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
