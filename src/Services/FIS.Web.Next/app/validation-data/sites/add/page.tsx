import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { createSiteAction } from "@/app/validation-data/sites/actions";
import SiteForm from "@/app/validation-data/sites/site-form";
import { getSiteReferenceData, SiteApiError, type SiteRecord } from "@/lib/api-sites";
import { getSession } from "@/lib/session";

const emptySite: SiteRecord = {
  siteCode: 0,
  departmentCode: null,
  description: "",
  responsiblePerson: null,
  address1: null,
  address2: null,
  address3: null,
  postalCode: null,
  telephone: null,
  telephone2: null,
  fax: null,
  fax1: null,
  netAddress: null,
  departmentNumber: "",
  mapReference: null,
  mapDescription: null,
  cellNumber: null,
  siteActive: true,
  financialSystemCode: null,
  financialSystemActive: null,
  financialSystemActivateDate: null,
  exportIsActive: null,
  dateLastExported: null,
  serviceKilometres: 0,
  serviceYears: 0,
  overheadPercentage: 0,
  provinceCode: null,
  notes: null,
  userAccessCode: null,
  modifiedByUserCode: null,
  dateCreated: null,
  dateUpdated: null,
};
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

export default async function SiteAddPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_SiteAdd.aspx" />
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
        <ErrorCard message="You do not have permission to add sites." />
      </main>
    );
  try {
    const referenceData = await getSiteReferenceData();
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="site-add-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Organisation</p>
              <h1 id="site-add-title">Add New Site</h1>
              <p>Add a complete site record to the legacy site table.</p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_Site.aspx">
              Site Maintenance
            </Link>
          </header>
          <SiteForm
            action={createSiteAction}
            site={emptySite}
            mode="create"
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
          <SessionRecovery returnPath="/Validation/MNT_SiteAdd.aspx" />
        </main>
      );
    console.error(
      "FIS site add reference data request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="The site reference data could not be loaded." />
      </main>
    );
  }
}
