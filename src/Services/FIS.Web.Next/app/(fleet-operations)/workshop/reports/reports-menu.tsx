import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";
import ApiUnavailablePage from "@/components/app-shell/api-unavailable-page";
import ReportsMenuLayout from "@/components/ui/reports-menu-layout";

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

async function WorkshopReportsMenuContent({
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
    return <ApiUnavailablePage message="Workshop reports are unavailable." retryHref={routePath} />;
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
    <ReportsMenuLayout
      headingId="workshop-reports-title"
      eyebrow="Workshop"
      title="Workshop Reports Menu"
      description="Run the legacy Workshop report workflows against compatible data."
      backHref={backHref}
      linkBase={linkBase}
      links={REPORT_LINKS}
    />
  );
}

export default function WorkshopReportsMenu(props: WorkshopReportsMenuProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <WorkshopReportsMenuContent {...props} />
    </Suspense>
  );
}
