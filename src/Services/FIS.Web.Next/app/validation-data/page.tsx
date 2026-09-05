import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

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

  const links = [
    ["Organisation Departments", "/validation-data/departments"],
    ["Organisation Sites", "/reference-data?tab=sites"],
    ["Vehicle Makes", "/validation-data/makes"],
    ["Vehicle Models", "/reference-data?tab=models"],
    ["Vehicle Types", "/reference-data?tab=types"],
    ["Vehicle Classes", "/reference-data?tab=classes"],
    ["Fuel Types", "/reference-data?tab=fueltypes"],
    ["Units of Measure", "/reference-data?tab=units"],
    ["License Types", "/reference-data?tab=licenses"],
    ["License Fees", "/reference-data?tab=licensefees"],
    ["Driver Licenses", "/reference-data?tab=driverlicenses"],
    ["Loss Types", "/reference-data?tab=losstypes"],
  ] as const;

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
          {links.map(([label, href]) => (
            <section className="vehicle-menu-tile" key={href}>
              <div className="vehicle-menu-body">
                <Link className="vehicle-menu-link" href={href}>{label}</Link>
              </div>
            </section>
          ))}
        </div>
      </section>
    </main>
  );
}
