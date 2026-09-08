import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { FuelCardNotice } from "@/app/fuel-cards/_components";
import { getFuelCardAllocation, FuelCardApiError } from "@/lib/api-fuel-cards";
import { getSession } from "@/lib/session";

const FUEL_CARDS_ROLE = "Fuelcards";

function hasRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(FUEL_CARDS_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

export default async function FuelCardsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/fuel-cards" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/fuel-cards" />
      </main>
    );
  if (!hasRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to access Fuelcards.</h2>
          <p className="muted-copy">This menu requires the Fuelcards role.</p>
        </section>
      </main>
    );

  const query = await searchParams;
  let preview: Awaited<ReturnType<typeof getFuelCardAllocation>> | null = null;
  let previewUnavailable = false;
  try {
    preview = await getFuelCardAllocation();
  } catch (error) {
    previewUnavailable = error instanceof FuelCardApiError && error.reason === "unavailable";
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="fuel-cards-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Fuelcards</p>
            <h1 id="fuel-cards-title">Fuelcard Maintenance Menu</h1>
            <p>Maintain fuelcards and open the legacy-compatible reports.</p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>
        <FuelCardNotice query={query} />
        <div className="vehicle-menu-tiles">
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Fuelcard Maintenance Information / Help</h2>
            <div className="vehicle-menu-body">
              <Link className="vehicle-menu-link" href="/fuel-cards/help">
                Open Fuelcard Maintenance Help
              </Link>
            </div>
          </section>
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Fuelcard Maintenance</h2>
            <div className="vehicle-menu-body">
              <Link className="vehicle-menu-link" href="/fuel-cards/vehicle">
                1) Fuelcard Maintenance for a Vehicle
              </Link>
              <Link className="vehicle-menu-link" href="/fuel-cards/collection-multiple">
                2) Collection for TWO or MORE Fuelcards
              </Link>
              <Link className="vehicle-menu-link" href="/fuel-cards/delete">
                3) Delete a Fuelcard
              </Link>
            </div>
          </section>
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Fuelcards Report</h2>
            <div className="vehicle-menu-body">
              <Link className="vehicle-menu-link" href="/fuel-cards/report/latest">
                1) Latest Fuelcard Report for a GG Vehicle
              </Link>
              <Link className="vehicle-menu-link" href="/reports/fuel-cards">
                Open Fuelcard Reports Menu
              </Link>
            </div>
          </section>
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Private Hire Vehicle Fuelcards - Maintenance</h2>
            <div className="vehicle-menu-body">
              <Link className="vehicle-menu-link" href="/fuel-cards/private-hire/vehicle">
                1) Private Hire Vehicle Fuelcards for a Vehicle
              </Link>
              <Link
                className="vehicle-menu-link"
                href="/fuel-cards/private-hire/collection-multiple"
              >
                2) Collection for TWO or MORE Fuelcards
              </Link>
              <Link className="vehicle-menu-link" href="/fuel-cards/private-hire/delete">
                3) Delete a Fuelcard
              </Link>
            </div>
          </section>
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Private Hire Vehicle Fuelcards - Reports</h2>
            <div className="vehicle-menu-body">
              <Link className="vehicle-menu-link" href="/fuel-cards/private-hire/report-all">
                1) Report for ALL Private Hire Vehicle Fuelcards
              </Link>
            </div>
          </section>
        </div>
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="fuel-card-preview-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Live records</p>
              <h2 id="fuel-card-preview-title">Fuelcard Activity Preview</h2>
            </div>
          </div>
          {previewUnavailable ? (
            <p className="muted-copy">Fuelcard activity is temporarily unavailable.</p>
          ) : preview && preview.recentActivity.length > 0 ? (
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <caption className="sr-only">Recent fuelcard activity</caption>
                <thead>
                  <tr>
                    <th scope="col">Card No.</th>
                    <th scope="col">GG Code</th>
                    <th scope="col">Action</th>
                    <th scope="col">Date</th>
                    <th scope="col">Receiver</th>
                  </tr>
                </thead>
                <tbody>
                  {preview.recentActivity.map((item, index) => (
                    <tr key={`${item.cardNumber ?? "card"}-${item.date}-${index}`}>
                      <td>{item.cardNumber ?? "-"}</td>
                      <td>{item.vmfCode}</td>
                      <td>{item.action}</td>
                      <td>{item.date.slice(0, 16).replace("T", " ") || "-"}</td>
                      <td>{item.receiver}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="muted-copy">No recent fuelcard activity is available.</p>
          )}
        </section>
      </section>
    </main>
  );
}
