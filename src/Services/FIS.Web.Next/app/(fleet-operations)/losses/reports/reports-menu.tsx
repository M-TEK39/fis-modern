import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";

const REPORT_LINKS = [
  [
    "vehicle",
    "Losses for One Vehicle",
    "Show all loss entries for one fleet or registration number sorted by loss date.",
  ],
  ["all", "All Losses", "View all losses sorted by loss type, date, and department."],
  [
    "no-report",
    "Losses Without Department Reports",
    "List entries where the departmental report is still outstanding.",
  ],
  ["with-report", "Losses With Reports", "List entries where a departmental report was supplied."],
  [
    "dept-period",
    "Losses for a Department and Period",
    "Filter losses by department, date range, and hire type.",
  ],
] as const;

export type LossReportsMenuProps = { routePath?: string; linkBase?: string; backHref?: string };

function hasLossReportAccess(roles: readonly string[]) {
  return roles.some((role) =>
    ["Losses", "Reports"].some(
      (expected) => role.localeCompare(expected, undefined, { sensitivity: "accent" }) === 0,
    ),
  );
}

export default async function LossReportsMenu({
  routePath = "/losses/reports",
  linkBase = routePath,
  backHref = "/losses",
}: LossReportsMenuProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h1>Losses reports are unavailable.</h1>
          <p className="muted-copy">Retry when the FIS API is available.</p>
          <Link className="button button-primary" href={routePath}>
            Try again
          </Link>
        </section>
      </main>
    );
  }
  if (!hasLossReportAccess(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h1>You do not have permission to run Losses reports.</h1>
        </section>
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="loss-reports-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Losses</p>
            <h1 id="loss-reports-title">Losses Reports Menu</h1>
            <p>Legacy loss report filters and outputs, rendered from the compatible FIS API.</p>
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
