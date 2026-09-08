import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";
import { searchTroubleshootOdometer, TroubleshootApiError } from "@/lib/api-troubleshoot";
import { hasTroubleshootingRole, Pagination, StatusCard, TroubleshootMenu, TroubleshootShell, valueOrDash } from "@/app/troubleshoot/_components";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type SearchMode = "GG" | "REG" | "TA";

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
function modeValue(value: string | undefined): SearchMode {
  return value === "REG" || value === "TA" ? value : "GG";
}

export default async function OdometerCorrectionsPage({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/troubleshoot/odometer-corrections" /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><StatusCard title="API unavailable" message="The sign-in service is temporarily unavailable." href="/troubleshoot/odometer-corrections" /></main>;
  if (!hasTroubleshootingRole(session.roles)) return <main className="page-shell vehicle-page-shell"><StatusCard title="Access restricted" message="You do not have permission to access Troubleshoot." href="/home" /></main>;

  const query = await searchParams;
  const mode = modeValue(first(query.mode));
  const searchValue = (first(query.value) ?? "").trim();
  const submitted = first(query.submitted) === "1";
  const page = Number(first(query.page)) > 0 ? Number(first(query.page)) : 1;
  let results = [] as Awaited<ReturnType<typeof searchTroubleshootOdometer>>;
  let errorMessage: string | null = null;
  if (submitted) {
    if (!searchValue) errorMessage = "Please enter a GG/Registration Number or Trip Authority Number.";
    else if (mode === "TA" && !/^\d+$/.test(searchValue)) errorMessage = "Please enter a valid Trip Authority Number.";
    else {
      try {
        results = await searchTroubleshootOdometer({ searchMode: mode, searchValue });
      } catch (error) {
        errorMessage = error instanceof TroubleshootApiError ? error.message : "Odometer corrections could not be loaded.";
      }
    }
  }
  const pageSize = 12;
  const totalPages = Math.max(1, Math.ceil(results.length / pageSize));
  const currentPage = Math.min(page, totalPages);
  const visibleResults = results.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  return <TroubleshootShell title="ODOMeter Corrections" description="Search and review vehicle odometer corrections."><TroubleshootMenu /><section className="vehicle-status-maintenance-panel" aria-labelledby="odometer-search-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Search criteria</p><h2 id="odometer-search-title">Find vehicle or trip authority</h2></div></div><form className="vehicle-create-form" method="get"><input type="hidden" name="submitted" value="1" /><div className="form-grid"><div className="form-field"><label className="form-label" htmlFor="odometer-mode">Search criteria</label><select className="form-select" id="odometer-mode" name="mode" defaultValue={mode}><option value="GG">Vehicle GG Number</option><option value="REG">Vehicle Registration</option><option value="TA">Trip Authority No.</option></select></div><div className="form-field"><label className="form-label" htmlFor="odometer-value">Value</label><input className="form-input" id="odometer-value" name="value" defaultValue={searchValue} required /></div></div><div className="button-row"><button className="button button-primary" type="submit">Submit Details</button><a className="button button-secondary" href="/troubleshoot/odometer-corrections">Clear</a></div></form></section>{errorMessage ? <div className="notice notice-error" role="alert">{errorMessage}</div> : null}{!submitted ? <div className="vehicle-empty-state"><p>Provide search criteria to locate odometer corrections.</p></div> : errorMessage ? null : results.length === 0 ? <div className="vehicle-empty-state"><p>No results found.</p></div> : <section className="vehicle-status-maintenance-panel" aria-labelledby="odometer-results-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">{results.length} result{results.length === 1 ? "" : "s"}</p><h2 id="odometer-results-title">Odometer results</h2></div></div><div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Odometer correction results</caption><thead><tr><th scope="col">Vehicle</th><th scope="col">Trip Authority</th><th scope="col">Current Odometer</th><th scope="col">Last Odometer</th></tr></thead><tbody>{visibleResults.map((row, index) => <tr key={`${row.vehicleIdentifier ?? "vehicle"}-${row.tripAuthorityNumber ?? index}`}><td>{valueOrDash(row.vehicleIdentifier)}</td><td>{valueOrDash(row.tripAuthorityNumber)}</td><td>{valueOrDash(row.currentOdometer)}</td><td>{valueOrDash(row.lastOdometer)}</td></tr>)}</tbody></table></div><Pagination path="/troubleshoot/odometer-corrections" page={currentPage} totalPages={totalPages} query={{ submitted: 1, mode, value: searchValue }} /></section>}</TroubleshootShell>;
}
