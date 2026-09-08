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
import {
  getPrivateHireContractors,
  getPrivateHireVehicles,
  PrivateHireApiError,
  type PrivateHireContractorRecord,
  type PrivateHireVehicleRecord,
} from "@/lib/api-private-hire";
import { getSession } from "@/lib/session";

const ROLE = "Private Hire Vehicles";
export type PrivateHireReportKind =
  "all" | "one" | "site" | "department" | "department-in-service" | "company";

function hasRole(roles: readonly string[]) {
  return roles.some((role) => role.localeCompare(ROLE, undefined, { sensitivity: "accent" }) === 0);
}

function getNumber(value: string) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function reportTitle(kind: PrivateHireReportKind) {
  return {
    all: "Report on All Private Hire Vehicles",
    one: "Report on One Private Hire Vehicle",
    site: "Private Hire Vehicles per Site",
    department: "Private Hire Vehicles per Department",
    "department-in-service": "Private Hire Vehicles per Department in Service",
    company: "Private Hire Vehicles per Hire Company",
  }[kind];
}

function ReportTable({
  vehicles,
  contractors,
}: Readonly<{ vehicles: PrivateHireVehicleRecord[]; contractors: PrivateHireContractorRecord[] }>) {
  const contractorNames = new Map(
    contractors.map((contractor) => [contractor.contractorId, contractor.companyName]),
  );
  if (vehicles.length === 0)
    return <p className="muted-copy">No records found for the selected report.</p>;
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Private Hire vehicle report</caption>
        <thead>
          <tr>
            <th scope="col">Registration</th>
            <th scope="col">Model description</th>
            <th scope="col">Engine no.</th>
            <th scope="col">Chassis no.</th>
            <th scope="col">Site</th>
            <th scope="col">Contracted to</th>
            <th scope="col">Year</th>
            <th scope="col">Colour</th>
            <th scope="col">Take-on</th>
            <th scope="col">Take-on odo</th>
            <th scope="col">Return</th>
            <th scope="col">Return odo</th>
          </tr>
        </thead>
        <tbody>
          {vehicles.slice(0, 500).map((vehicle) => (
            <tr key={vehicle.phvCode}>
              <td>{valueOrDash(vehicle.registrationNumber)}</td>
              <td>{valueOrDash(vehicle.modelDescription)}</td>
              <td>{valueOrDash(vehicle.engineNumber)}</td>
              <td>{valueOrDash(vehicle.chassisNumber)}</td>
              <td>{valueOrDash(vehicle.siteCode)}</td>
              <td>
                {valueOrDash(
                  contractorNames.get(vehicle.contractorId) ??
                    vehicle.contractedTo ??
                    vehicle.contractorId,
                )}
              </td>
              <td>{valueOrDash(vehicle.yearManufactured)}</td>
              <td>{valueOrDash(vehicle.colour)}</td>
              <td>{dateValue(vehicle.takeOnDate)}</td>
              <td>{valueOrDash(vehicle.takeOnOdo)}</td>
              <td>{dateValue(vehicle.returnDate)}</td>
              <td>{valueOrDash(vehicle.returnOdo)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ReportFilter({
  kind,
  query,
  contractors,
}: Readonly<{
  kind: PrivateHireReportKind;
  query: Record<string, string | string[] | undefined>;
  contractors: PrivateHireContractorRecord[];
}>) {
  if (kind === "all") return null;
  const search = queryValue(query.search);
  const code = queryValue(query.code);
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report filter</p>
          <h2>Choose the report scope</h2>
        </div>
      </div>
      {kind === "one" ? (
        <div className="vehicle-search-row">
          <label className="sr-only" htmlFor="private-hire-report-search">
            Vehicle search
          </label>
          <input
            className="vehicle-search"
            id="private-hire-report-search"
            name="search"
            defaultValue={search}
            placeholder="Registration, engine, chassis, or PHV code"
          />
          <button className="button button-primary" type="submit">
            Run report
          </button>
        </div>
      ) : kind === "company" ? (
        <div className="vehicle-search-row">
          <label className="form-label" htmlFor="private-hire-report-company">
            Hire company
          </label>
          <select
            className="form-select"
            id="private-hire-report-company"
            name="code"
            defaultValue={code}
          >
            <option value="">Select a hire company</option>
            {contractors.map((contractor) => (
              <option key={contractor.contractorId} value={contractor.contractorId}>
                {contractor.companyName} ({contractor.contractorId})
              </option>
            ))}
          </select>
          <button className="button button-primary" type="submit">
            Run report
          </button>
        </div>
      ) : (
        <div className="vehicle-search-row">
          <label className="form-label" htmlFor="private-hire-report-code">
            {kind === "site" ? "Site code" : "Department / contracted-to code"}
          </label>
          <input
            className="vehicle-search"
            id="private-hire-report-code"
            name="code"
            type="number"
            min="1"
            defaultValue={code}
            required
          />
          <button className="button button-primary" type="submit">
            Run report
          </button>
        </div>
      )}
    </form>
  );
}

export default async function PrivateHireReportPage({
  searchParams,
  kind = "all",
  routePath = "/private-hire/reports/all-vehicles",
}: Readonly<{
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
  kind?: PrivateHireReportKind;
  routePath?: string;
}>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (!hasRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to view Private Hire reports.</h2>
        </section>
      </main>
    );

  const query = searchParams ? await searchParams : {};
  try {
    const [vehicles, contractors] = await Promise.all([
      getPrivateHireVehicles(),
      getPrivateHireContractors(),
    ]);
    const code = getNumber(queryValue(query.code));
    const search = queryValue(query.search).trim().toLowerCase();
    const filtered =
      kind === "one" && search
        ? vehicles.filter((vehicle) =>
            [
              String(vehicle.phvCode),
              vehicle.registrationNumber,
              vehicle.engineNumber ?? "",
              vehicle.chassisNumber ?? "",
            ].some((value) => value.toLowerCase().includes(search)),
          )
        : kind === "site" && code
          ? vehicles.filter((vehicle) => vehicle.siteCode === code)
          : (kind === "department" || kind === "department-in-service") && code
            ? vehicles.filter(
                (vehicle) =>
                  vehicle.contractedTo === code &&
                  (kind !== "department-in-service" || vehicle.returnDate === null),
              )
            : kind === "company" && code
              ? vehicles.filter((vehicle) => vehicle.contractorId === code)
              : kind === "one" && !search
                ? []
                : vehicles;

    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="private-hire-report-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Private Hire reports</p>
              <h1 id="private-hire-report-title">{reportTitle(kind)}</h1>
              <p>Date of report: {new Date().toLocaleDateString()}</p>
            </div>
            <Link className="button button-secondary" href="/private-hire/maintenance-menu">
              Menu
            </Link>
          </header>
          <PrivateHireNotice query={query} />
          <ReportFilter kind={kind} query={query} contractors={contractors} />
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="private-hire-report-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Report results</p>
                <h2 id="private-hire-report-results-title">
                  {filtered.length} record{filtered.length === 1 ? "" : "s"}
                </h2>
              </div>
            </div>
            <ReportTable vehicles={filtered} contractors={contractors} />
          </section>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof PrivateHireApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable path={routePath} subject="Private Hire reports" />
      </main>
    );
  }
}

export async function PrivateHireReportRoute({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <>{await PrivateHireReportPage({ searchParams })}</>;
}
