import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import ApiUnavailablePage from "@/components/app-shell/api-unavailable-page";
import ReportsMenuLayout from "@/components/ui/reports-menu-layout";
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
    return <ApiUnavailablePage message="Losses reports are unavailable." retryHref={routePath} />;
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
    <ReportsMenuLayout
      headingId="loss-reports-title"
      eyebrow="Losses"
      title="Losses Reports Menu"
      description="Legacy loss report filters and outputs, rendered from the compatible FIS API."
      backHref={backHref}
      linkBase={linkBase}
      links={REPORT_LINKS}
    />
  );
}
