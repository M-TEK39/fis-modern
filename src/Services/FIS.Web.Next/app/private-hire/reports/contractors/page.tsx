import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import {
  ApiUnavailable,
  PrivateHireNotice,
  dateValue,
  queryValue,
  valueOrDash,
} from "@/app/private-hire/_components";
import { getPrivateHireContractors, PrivateHireApiError } from "@/lib/api-private-hire";
import { getSession } from "@/lib/session";

const ROLE = "Private Hire Vehicles";

function hasRole(roles: readonly string[]) {
  return roles.some((role) => role.localeCompare(ROLE, undefined, { sensitivity: "accent" }) === 0);
}

export default async function PrivateHireContractorReportPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/private-hire/reports/contractors" />
      </main>
    );
  if (!hasRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to view Private Hire contractor reports.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const scope = queryValue(query.scope) === "active" ? "active" : "all";
  try {
    const contractors = await getPrivateHireContractors();
    const filtered =
      scope === "active"
        ? contractors.filter((contractor) => contractor.status !== "Inactive")
        : contractors;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="private-hire-contractor-report-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Private Hire reports</p>
              <h1 id="private-hire-contractor-report-title">Report on Private Hire Contractors</h1>
              <p>Select contractor scope for the report.</p>
            </div>
            <Link className="button button-secondary" href="/private-hire/maintenance-menu">
              Menu
            </Link>
          </header>
          <PrivateHireNotice query={query} />
          <form className="vehicle-status-maintenance-panel" method="get">
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Report filter</p>
                <h2>Contractor scope</h2>
              </div>
            </div>
            <div className="vehicle-search-row">
              <label className="form-label" htmlFor="private-hire-contractor-scope">
                Show
              </label>
              <select
                className="form-select"
                id="private-hire-contractor-scope"
                name="scope"
                defaultValue={scope}
              >
                <option value="all">All contractors</option>
                <option value="active">Active Private Hire contractors</option>
              </select>
              <button className="button button-primary" type="submit">
                Run report
              </button>
            </div>
          </form>
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="private-hire-contractor-report-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Report results</p>
                <h2 id="private-hire-contractor-report-results-title">
                  {filtered.length} contractor{filtered.length === 1 ? "" : "s"}
                </h2>
              </div>
            </div>
            {filtered.length === 0 ? (
              <p className="muted-copy">No records found for the selected report.</p>
            ) : (
              <div className="vehicle-table-wrapper">
                <table className="vehicle-table">
                  <caption className="sr-only">Private Hire contractor report</caption>
                  <thead>
                    <tr>
                      <th scope="col">Contractor name</th>
                      <th scope="col">Physical address</th>
                      <th scope="col">Postal address</th>
                      <th scope="col">Telephone</th>
                      <th scope="col">Fax</th>
                      <th scope="col">Email</th>
                      <th scope="col">Contact person</th>
                      <th scope="col">Quotations</th>
                      <th scope="col">Type</th>
                      <th scope="col">Project name</th>
                      <th scope="col">Project begin</th>
                      <th scope="col">Project end</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filtered.slice(0, 500).map((contractor) => (
                      <tr key={contractor.contractorId}>
                        <td>{valueOrDash(contractor.companyName)}</td>
                        <td>{valueOrDash(contractor.physicalAddress)}</td>
                        <td>{valueOrDash(contractor.postalAddress)}</td>
                        <td>{valueOrDash(contractor.phone)}</td>
                        <td>{valueOrDash(contractor.faxNumber)}</td>
                        <td>{valueOrDash(contractor.email)}</td>
                        <td>{valueOrDash(contractor.contactPerson)}</td>
                        <td>
                          {contractor.quotations === null
                            ? "-"
                            : contractor.quotations
                              ? "Yes"
                              : "No"}
                        </td>
                        <td>{valueOrDash(contractor.type)}</td>
                        <td>{valueOrDash(contractor.projectName)}</td>
                        <td>{dateValue(contractor.projectBeginDate)}</td>
                        <td>{dateValue(contractor.projectEndDate)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof PrivateHireApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/private-hire/reports/contractors" />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable
          path="/private-hire/reports/contractors"
          subject="Private Hire contractor reports"
        />
      </main>
    );
  }
}
