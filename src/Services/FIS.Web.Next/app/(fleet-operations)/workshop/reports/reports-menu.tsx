import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";

const REPORT_LINKS = [
  [
    "one-vehicle",
    "Workshop Report on One Vehicle",
    "Search Workshop entries by a partial GG or GP number.",
  ],
  [
    "print-job-card",
    "Print a Workshop Job Card",
    "Find a vehicle or select a job-card number to review its compatible fields.",
  ],
  [
    "period",
    "Workshop Report for a Period",
    "Filter received entries by date range, garage, and accident/mechanical category.",
  ],
  [
    "in-workshop",
    "List of Vehicles Still in Workshop",
    "List open job cards with elapsed days and hours.",
  ],
  ["merchants", "List of All Merchants", "Review the Workshop-specific merchant directory."],
] as const;

export type WorkshopReportsMenuProps = {
  routePath?: string;
  linkBase?: string;
  backHref?: string;
};

function hasWorkshopReportAccess(roles: readonly string[]) {
  return roles.some((role) =>
    ["Workshop", "Reports"].some(
      (expected) => role.localeCompare(expected, undefined, { sensitivity: "accent" }) === 0,
    ),
  );
}

export default async function WorkshopReportsMenu({
  routePath = "/workshop/reports",
  linkBase = routePath,
  backHref = "/workshop",
}: WorkshopReportsMenuProps) {
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
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h1>Workshop reports are unavailable.</h1>
          <p className="muted-copy">Retry when the FIS API is available.</p>
          <Link className="button button-primary" href={routePath}>
            Try again
          </Link>
        </section>
      </main>
    );
  if (!hasWorkshopReportAccess(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h1>You do not have permission to run Workshop reports.</h1>
        </section>
      </main>
    );

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="workshop-reports-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Workshop</p>
            <h1 id="workshop-reports-title">Workshop Reports Menu</h1>
            <p>Run the legacy Workshop report workflows against compatible data.</p>
          </div>
          <Link className="button button-secondary" href={backHref}>
            Back
          </Link>
        </header>
        <div className="vehicle-menu-tiles">
          {REPORT_LINKS.map(([mode, title, description]) => (
            <section className="vehicle-menu-tile" key={mode}>
              <h2 className="vehicle-menu-header">
                <Link href={`${linkBase}/${mode}`}>{title}</Link>
              </h2>
              <div className="vehicle-menu-body">
                <p className="muted-copy">{description}</p>
                <Link className="button button-primary button-small" href={`${linkBase}/${mode}`}>
                  Open report
                </Link>
              </div>
            </section>
          ))}
        </div>
      </section>
    </main>
  );
}
