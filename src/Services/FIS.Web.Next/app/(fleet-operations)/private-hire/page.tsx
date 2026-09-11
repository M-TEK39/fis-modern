import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { MenuSection } from "@/components/ui/menu-section";
import {
  ApiUnavailable,
  PrivateHirePagination,
  PrivateHireNotice,
  valueOrDash,
  dateValue,
} from "@/app/(fleet-operations)/private-hire/_components";
import {
  DEFAULT_PRIVATE_HIRE_PAGE_SIZE,
  PrivateHireApiError,
  getPrivateHireContractors,
  getPrivateHirePage,
} from "@/lib/api/fleet-operations/api-private-hire";
import { getSession } from "@/lib/auth/session";

const PRIVATE_HIRE_ROLE = "Private Hire Vehicles";

function hasRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(PRIVATE_HIRE_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function requestedPage(value: string | string[] | undefined) {
  const candidate = Number(Array.isArray(value) ? value[0] : value);
  return Number.isInteger(candidate) && candidate > 0 ? candidate : 1;
}

async function PrivateHirePageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/private-hire" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/private-hire" />
      </main>
    );
  if (!hasRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to access Private Hire Vehicles.</h2>
          <p className="muted-copy">This menu requires the Private Hire Vehicles role.</p>
        </section>
      </main>
    );

  const query = await searchParams;
  try {
    const [vehiclePage, contractors] = await Promise.all([
      getPrivateHirePage({
        page: requestedPage(query.page),
        pageSize: DEFAULT_PRIVATE_HIRE_PAGE_SIZE,
      }),
      getPrivateHireContractors(),
    ]);
    const contractorNames = new Map(
      contractors.map((contractor) => [contractor.contractorId, contractor.companyName]),
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="private-hire-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Private Hire Vehicles</p>
              <h1 id="private-hire-title">Private Hire Menu</h1>
              <p>
                Maintain private hire vehicles and contractors using the legacy-compatible business
                process.
              </p>
            </div>
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          </header>
          <PrivateHireNotice query={query} />
          <div className="vehicle-menu-tiles">
            <MenuSection title="Maintenance">
              <Link className="vehicle-menu-link" href="/private-hire/maintenance-menu">
                1) Maintain Private Hire Information
              </Link>
              <Link className="vehicle-menu-link" href="/vehicles/demo/menu">
                2) Maintain Demo Vehicle Information
              </Link>
            </MenuSection>
            <MenuSection title="Reports">
              <Link className="vehicle-menu-link" href="/private-hire/reports/all-vehicles">
                Report on all Private Hire vehicles
              </Link>
              <Link className="vehicle-menu-link" href="/private-hire/reports/one-vehicle">
                Report on one Private Hire vehicle
              </Link>
              <Link className="vehicle-menu-link" href="/private-hire/reports/contractors">
                Report on Private Hire contractors
              </Link>
            </MenuSection>
            <MenuSection title="Help">
              <Link className="vehicle-menu-link" href="/private-hire/help">
                Open Private Hire help
              </Link>
            </MenuSection>
          </div>
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="private-hire-preview-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Live records</p>
                <h2 id="private-hire-preview-title">Private Hire vehicle preview</h2>
              </div>
              <span className="muted-copy">{vehiclePage.total} active or recently returned</span>
            </div>
            {vehiclePage.total === 0 ? (
              <p className="muted-copy">No active Private Hire vehicles are available.</p>
            ) : (
              <div className="vehicle-table-wrapper">
                <table className="vehicle-table">
                  <caption className="sr-only">Private Hire vehicle preview</caption>
                  <thead>
                    <tr>
                      <th scope="col">Registration</th>
                      <th scope="col">Model</th>
                      <th scope="col">Site</th>
                      <th scope="col">Contractor</th>
                      <th scope="col">Take-on</th>
                      <th scope="col">Return</th>
                    </tr>
                  </thead>
                  <tbody>
                    {vehiclePage.items.map((vehicle) => (
                      <tr key={vehicle.phvCode}>
                        <td>{valueOrDash(vehicle.registrationNumber)}</td>
                        <td>{valueOrDash(vehicle.modelDescription)}</td>
                        <td>{valueOrDash(vehicle.siteCode)}</td>
                        <td>
                          {valueOrDash(
                            contractorNames.get(vehicle.contractorId) ?? vehicle.contractorId,
                          )}
                        </td>
                        <td>{dateValue(vehicle.takeOnDate)}</td>
                        <td>{dateValue(vehicle.returnDate)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <PrivateHirePagination
              path="/private-hire"
              query={query}
              page={vehiclePage.page}
              totalPages={vehiclePage.totalPages}
            />
          </section>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof PrivateHireApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/private-hire" />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable path="/private-hire" />
      </main>
    );
  }
}

export default function PrivateHirePage(props: Parameters<typeof PrivateHirePageContent>[0]) {
  return (
    <StreamedRoute>
      <PrivateHirePageContent {...props} />
    </StreamedRoute>
  );
}
