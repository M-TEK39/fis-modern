import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { FuelCardApiError, getFuelCardAllocation } from "@/lib/api/fleet-operations/api-fuel-cards";
import { getSession } from "@/lib/auth/session";

const REPORTS = [
  ["one-vehicle", "Fuelcard Report for a Vehicle"],
  ["one-pan", "Fuelcard Report for a PAN Number"],
  ["one-vehicle-handout", "Fuelcard Handout Report"],
  ["all-vehicles", "Fuelcard Report for ALL Vehicles"],
  ["expire-date", "Fuelcard Report for an Expire Date"],
  ["replace-reason", "Fuelcard Report for a Replace Reason"],
  ["dept-site-expire-period", "Fuelcard Report for a Department / Site, for an Expire Period"],
  ["dept-site", "Fuelcard Report for a Department / Site"],
  ["one-site-expire-date", "Fuelcard Report for a Dept Site and Expire Date"],
  ["pool-vehicles", "Fuelcard Report for POOL Vehicles"],
  ["vip-vehicles", "Fuelcard Report for VIP Vehicles"],
  ["wesbank-new-cards", "Wesbank Application for New FuelCards"],
] as const;

function hasRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare("Fuelcards", undefined, { sensitivity: "accent" }) === 0,
  );
}

async function FuelCardReportsPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/reports/fuel-cards" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/reports/fuel-cards" />
      </main>
    );
  if (!hasRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to access Fuelcard reports.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const view = Array.isArray(query.view) ? query.view[0] : query.view;
  let report: Awaited<ReturnType<typeof getFuelCardAllocation>> | null = null;
  let error = "";
  if (view === "result") {
    try {
      report = await getFuelCardAllocation();
    } catch (requestError) {
      error =
        requestError instanceof FuelCardApiError && requestError.reason === "unavailable"
          ? "The fuelcard report service is temporarily unavailable."
          : "The fuelcard report could not be generated.";
    }
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="fuel-card-reports-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Fuelcards</p>
            <h1 id="fuel-card-reports-title">Fuelcards Report Menu</h1>
            <p>Choose a legacy-compatible fuelcard report.</p>
          </div>
          <Link className="button button-secondary" href="/fuel-cards">
            Menu
          </Link>
        </header>
        {error ? (
          <div className="notice notice-error" role="alert">
            {error}
          </div>
        ) : null}
        <div className="vehicle-menu-tiles">
          {REPORTS.map(([key, label], index) => (
            <Link
              className="vehicle-menu-link"
              key={key}
              href={`/reports/fuel-cards?view=result&rtype=${key}`}
            >
              {index + 1}) {label}
            </Link>
          ))}
        </div>
        {report ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="fuel-card-report-result-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Live result</p>
                <h2 id="fuel-card-report-result-title">
                  Fuelcard activity ({report.recentActivity.length} recent rows)
                </h2>
              </div>
            </div>
            <div className="vehicle-stat-grid">
              <div>
                <strong>{report.totalCards}</strong>
                <span>Total cards</span>
              </div>
              <div>
                <strong>{report.activeCards}</strong>
                <span>In service</span>
              </div>
              <div>
                <strong>{report.returnedCards}</strong>
                <span>Returned / other</span>
              </div>
              <div>
                <strong>{report.expiringCards}</strong>
                <span>Expiring soon</span>
              </div>
            </div>
            {report.recentActivity.length === 0 ? (
              <p className="muted-copy">No report rows found.</p>
            ) : (
              <div className="vehicle-table-wrapper">
                <table className="vehicle-table">
                  <caption className="sr-only">Fuelcard report activity</caption>
                  <thead>
                    <tr>
                      <th scope="col">Date</th>
                      <th scope="col">Vehicle</th>
                      <th scope="col">Card Number</th>
                      <th scope="col">Action</th>
                      <th scope="col">Receiver</th>
                    </tr>
                  </thead>
                  <tbody>
                    {report.recentActivity.map((item, index) => (
                      <tr key={`${item.date}-${index}`}>
                        <td>{item.date.slice(0, 16).replace("T", " ") || "-"}</td>
                        <td>{item.vmfCode}</td>
                        <td>{item.cardNumber ?? "-"}</td>
                        <td>{item.action}</td>
                        <td>{item.receiver}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        ) : null}
      </section>
    </main>
  );
}

export default function FuelCardReportsPage(
  props: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>,
) {
  return (
    <StreamedRoute>
      <FuelCardReportsPageContent {...props} />
    </StreamedRoute>
  );
}
