import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

const VALIDATION_LINKS = [
  ["1) Department Maintenance", "/validation-data/departments"],
  ["2) Make Maintenance", "/validation-data/makes"],
  ["3) Model Maintenance", "/validation-data/models"],
  ["4) Site Maintenance", "/validation-data/sites"],
  ["5) Class codes Maintenance", "/validation-data/classes"],
  ["6) License Fees Maintenance", "/validation-data/license-fees"],
  ["7) Drivers license Maintenance", "/validation-data/driver-licenses"],
  ["9) Extras Maintenance", "/validation-data/extras"],
  ["10) Loss Description Maintenance", "/validation-data/loss-types"],
] as const;

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Validation Data.</h2>
      <Link className="button button-secondary" href="/home">Home</Link>
    </section>
  );
}

export default async function ValidationDataPage() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/validation-data" /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Service unavailable</p><h2>Validation Data could not be opened.</h2><p className="muted-copy">Retry when the FIS API is available.</p></section></main>;
  if (!hasVehicleManagementPermission(session.accessLevel)) return <main className="page-shell vehicle-page-shell"><AccessRestricted /></main>;

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="validation-data-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Validation</p>
            <h1 id="validation-data-title">Validation Data</h1>
            <p>Open the same validation-maintenance areas as the legacy menu.</p>
          </div>
          <Link className="button button-secondary" href="/home">Home</Link>
        </header>
        <div className="vehicle-menu-tiles">
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Validation Maintenance Information</h2>
            <div className="vehicle-menu-body">
              <Link className="vehicle-menu-link" href="/validation-data/help">Validation Data Maintenance Information / Help</Link>
            </div>
          </section>
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Validation Maintenance</h2>
            <div className="vehicle-menu-body">
              {VALIDATION_LINKS.map(([label, href]) => <Link className="vehicle-menu-link" href={href} key={href}>{label}</Link>)}
            </div>
          </section>
        </div>
      </section>
    </main>
  );
}
