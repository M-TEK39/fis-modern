import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

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
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h1>Asset Verification reports are unavailable.</h1>
          <p className="muted-copy">Retry when the FIS API is available.</p>
          <Link className="button button-primary" href={routePath}>
            Try again
          </Link>
        </section>
      </main>
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
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="asset-verification-reports-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Vehicle asset verification</p>
            <h1 id="asset-verification-reports-title">Asset Verification Reports Menu</h1>
            <p>Run the three legacy Asset Verification report workflows against compatible data.</p>
          </div>
          <Link className="button button-secondary" href={backHref}>
            Back
          </Link>
        </header>
        <div className="vehicle-menu-tiles">
          {REPORT_LINKS.map(([mode, title, description]) => (
            <section className="vehicle-menu-tile" key={mode}>
              <h2 className="vehicle-menu-header">
                <Link href={`${routePath}/${mode}`}>{title}</Link>
              </h2>
              <div className="vehicle-menu-body">
                <p className="muted-copy">{description}</p>
                <Link className="button button-primary button-small" href={`${routePath}/${mode}`}>
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
