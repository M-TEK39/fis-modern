import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";
import { redirect } from "next/navigation";

import {
  hasVehicleManagementPermission,
  getQueryValue,
} from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { deleteSiteAction } from "@/app/(administration)/validation-data/sites/actions";
import { getSite, getSiteDeleteCheck, SiteApiError } from "@/lib/api/reference-data/api-sites";
import { getSession } from "@/lib/auth/session";

type SiteDeleteCheckPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};
function parseCode(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 && parsed <= 32767 ? parsed : null;
}
function ErrorCard({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Site deletion</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_Site.aspx">
        Site Maintenance
      </Link>
    </section>
  );
}

async function renderSiteDeleteCheckPage({ searchParams }: SiteDeleteCheckPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Site_Del_Check.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Site deletion is temporarily unavailable." />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to delete sites." />
      </main>
    );
  const query = await searchParams;
  const siteCode = parseCode(
    getQueryValue(query.code) ?? getQueryValue(query.siteCode) ?? getQueryValue(query.cmbSite),
  );
  const error = getQueryValue(query.error);
  if (!siteCode)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Select a site before attempting deletion." />
      </main>
    );
  if (error)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message={error} />
      </main>
    );
  try {
    const [site, dependencies] = await Promise.all([
      getSite(siteCode),
      getSiteDeleteCheck(siteCode),
    ]);
    if (!site)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Site ${siteCode} was not found.`} />
        </main>
      );
    const blocked = dependencies.contractCount > 0;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="site-delete-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Organisation</p>
              <h1 id="site-delete-title">Delete Site</h1>
              <p>
                Check linked contracts before removing this site from the active maintenance list.
              </p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_Site.aspx">
              Site Maintenance
            </Link>
          </header>
          <section className="vehicle-status-card" role={blocked ? "alert" : "note"}>
            <p className="eyebrow">Site {site.siteCode}</p>
            <h2>{site.description || `Site ${site.siteCode}`}</h2>
            <dl className="status-maintenance-details">
              <div>
                <dt>Linked contracts</dt>
                <dd>{dependencies.contractCount}</dd>
              </div>
            </dl>
            {blocked ? (
              <>
                <p className="muted-copy">
                  Contracts issued to this site must be changed before deleting it.
                </p>
                <Link className="button button-secondary" href="/Validation/MNT_Site.aspx">
                  Return to Site Maintenance
                </Link>
              </>
            ) : (
              <form action={deleteSiteAction} className="button-row">
                <input name="siteCode" type="hidden" value={site.siteCode} readOnly />
                <button className="button button-primary" type="submit">
                  Confirm Delete
                </button>
                <Link className="button button-secondary" href="/Validation/MNT_Site.aspx">
                  Cancel
                </Link>
              </form>
            )}
          </section>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof SiteApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`/Validation/MNT_Site_Del_Check.aspx?code=${siteCode}`} />
        </main>
      );
    if (error instanceof SiteApiError && error.status === 404)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Site ${siteCode} was not found.`} />
        </main>
      );
    console.error(
      "FIS site delete check failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Site dependencies could not be checked." />
      </main>
    );
  }
}

export default function SiteDeleteCheckPage(props: SiteDeleteCheckPageProps) {
  return <Suspense fallback={<RouteLoading />}>{renderSiteDeleteCheckPage(props)}</Suspense>;
}
