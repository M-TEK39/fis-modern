import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import { getQueryValue } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { updateSiteAction } from "@/app/validation-data/sites/actions";
import SiteForm from "@/app/validation-data/sites/site-form";
import { getSite, getSiteReferenceData, SiteApiError } from "@/lib/api-sites";
import { getSession } from "@/lib/session";

type SiteEditPageProps = { searchParams: Promise<Record<string, string | string[] | undefined>> };
function parseCode(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 && parsed <= 32767 ? parsed : null;
}
function ErrorCard({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Site maintenance</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_Site.aspx">
        Site Maintenance
      </Link>
    </section>
  );
}

export default async function SiteEditPage({ searchParams }: SiteEditPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Site_Edit.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Site maintenance is temporarily unavailable." />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to edit sites." />
      </main>
    );
  const query = await searchParams;
  const siteCode = parseCode(
    getQueryValue(query.cmbSite) ?? getQueryValue(query.code) ?? getQueryValue(query.siteCode),
  );
  if (!siteCode)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Select a site before opening edit." />
      </main>
    );
  try {
    const [site, referenceData] = await Promise.all([getSite(siteCode), getSiteReferenceData()]);
    if (!site)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Site ${siteCode} was not found.`} />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="site-edit-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Organisation</p>
              <h1 id="site-edit-title">Edit Site</h1>
              <p>Update site {siteCode} without dropping legacy or expanded-schema values.</p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_Site.aspx">
              Site Maintenance
            </Link>
          </header>
          <SiteForm
            action={updateSiteAction}
            site={site}
            mode="update"
            referenceData={referenceData}
            returnPath="/Validation/MNT_Site.aspx"
          />
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof SiteApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`/Validation/MNT_Site_Edit.aspx?cmbSite=${siteCode}`} />
        </main>
      );
    if (error instanceof SiteApiError && error.status === 404)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Site ${siteCode} was not found.`} />
        </main>
      );
    console.error(
      "FIS site edit request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Site details could not be loaded." />
      </main>
    );
  }
}
