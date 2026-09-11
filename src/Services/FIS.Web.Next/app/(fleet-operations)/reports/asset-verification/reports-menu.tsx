import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import ApiUnavailablePage from "@/components/app-shell/api-unavailable-page";
import ReportsMenuLayout from "@/components/ui/reports-menu-layout";
import { getSession } from "@/lib/auth/session";

const REPORT_LINKS = [
  [
    "per-site-province-date",
    "Report Per Site / Province / Verification Date",
    "Filter verified asset records by site, province, or verification date.",
  ],
  [
    "not-verified",
    "Vehicles Not Verified",
    "List active vehicles with a current contract and no compatible verification record.",
  ],
  [
    "verified-by-date-range",
    "Vehicles Verified By Date Range - Excel Report",
    "List vehicle verification records captured within a date range.",
  ],
] as const;

export type AssetVerificationReportsMenuProps = {
  routePath?: string;
  backHref?: string;
};

function hasReportsRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare("Reports", undefined, { sensitivity: "accent" }) === 0,
  );
}

export default async function AssetVerificationReportsMenu({
  routePath = "/reports/asset-verification",
  backHref = "/vehicle-verification",
}: AssetVerificationReportsMenuProps) {
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
      <ApiUnavailablePage
        message="Asset Verification reports are unavailable."
        retryHref={routePath}
      />
    );
  }
  if (!hasReportsRole(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h1>You do not have permission to run Asset Verification reports.</h1>
        </section>
      </main>
    );
  }

  return (
    <ReportsMenuLayout
      headingId="asset-verification-reports-title"
      eyebrow="Vehicle asset verification"
      title="Asset Verification Reports Menu"
      description="Run the three legacy Asset Verification report workflows against compatible data."
      backHref={backHref}
      linkBase={routePath}
      links={REPORT_LINKS}
    />
  );
}
