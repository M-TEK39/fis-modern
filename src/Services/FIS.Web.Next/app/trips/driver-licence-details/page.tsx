import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getQueryValue, parsePositiveInteger } from "@/app/drivers/access";
import { hasTripAuthorityAccess, getTripSession, tripAccessRestricted, tripSessionMessage } from "@/app/trips/_page";
import {
  DriverManagementApiError,
  getDriverManagementLicenceTypes,
  getDriverManagementSiteDrivers,
  getDriverManagementSites,
  type DriverManagementDriver,
} from "@/lib/api-driver-management";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function displayName(driver: DriverManagementDriver) {
  const name = `${driver.driverFirstname ?? ""} ${driver.driverSurname ?? ""}`.trim();
  return name || `Site driver ${driver.siteDriverCode}`;
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function DetailTable({ driver, siteName, licenceType }: Readonly<{ driver: DriverManagementDriver; siteName: string; licenceType: string }>) {
  const details = [
    ["Site Driver ID", driver.siteDriverCode],
    ["Driver", displayName(driver)],
    ["South African ID", driver.driverSAId],
    ["Passport Number", driver.driverPassportNumber],
    ["Persal Number", driver.driverPersonalNumber],
    ["Contract Number", driver.driverContractNumber],
    ["Site", siteName],
    ["Licence Type", licenceType],
    ["Licence Number", driver.driverLicenceNumber],
    ["Licence Issue Date", formatDate(driver.driverLicenceIssueDate)],
    ["Licence Expiry Date", formatDate(driver.driverLicenceExpiryDate)],
    ["Last Verified", formatDate(driver.driverLicenceLastVerifiedDate)],
    ["PDP", driver.driverHasPDP ? "Yes" : "No"],
    ["PDP Expiry", formatDate(driver.driverPDPExpiryDate)],
    ["Driver Active", driver.driverActive ? "Yes" : "No"],
  ] as const;

  return <section className="vehicle-status-maintenance-panel" aria-labelledby="driver-licence-results-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Driver record</p><h2 id="driver-licence-results-title">Licence details for {displayName(driver)}</h2></div><span className="form-hint">{details.length} fields</span></div><div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Licence details for {displayName(driver)}</caption><thead><tr><th scope="col">Field</th><th scope="col">Value</th></tr></thead><tbody>{details.map(([label, value]) => <tr key={label}><th scope="row">{label}</th><td>{valueOrDash(value)}</td></tr>)}</tbody></table></div></section>;
}

function ApiUnavailable() {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>Driver licence details could not be loaded.</h2><p className="muted-copy">Retry when the FIS API is available.</p><Link className="button button-primary" href="/trips/driver-licence-details">Try again</Link></section>;
}

export default async function DriverLicenceDetailsPage({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getTripSession();
  const sessionProblem = tripSessionMessage(session, "/trips/driver-licence-details");
  if (sessionProblem) return sessionProblem;
  if (session.status !== "authenticated") return tripAccessRestricted("Your session could not be loaded.");
  if (!hasTripAuthorityAccess(session)) return tripAccessRestricted();

  const query = await searchParams;
  const selectedCode = parsePositiveInteger(getQueryValue(query.siteDriverCode) ?? getQueryValue(query.SiteDriverCode));

  try {
    const [drivers, sites, licenceTypes] = await Promise.all([
      getDriverManagementSiteDrivers(),
      getDriverManagementSites(),
      getDriverManagementLicenceTypes(),
    ]);
    const driver = selectedCode === null ? null : drivers.find((item) => item.siteDriverCode === selectedCode) ?? null;
    const site = driver === null ? null : sites.find((item) => item.code === driver.siteCode);
    const licenceType = driver === null ? null : licenceTypes.find((item) => item.id === driver.driverLicenceTypeId);

    return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="driver-licence-details-title"><header className="vehicle-page-header"><div><p className="eyebrow">Trip tools</p><h1 id="driver-licence-details-title">Driver Licence Details</h1><p>View driver licence details by site driver ID.</p></div><Link className="button button-secondary" href="/trip-authorities">Back to Trips</Link></header><form className="vehicle-status-maintenance-panel" method="get"><div className="form-grid"><div className="form-field"><label className="form-label" htmlFor="driver-licence-driver">Site Driver ID</label><select className="form-select" id="driver-licence-driver" name="siteDriverCode" defaultValue={selectedCode?.toString() ?? ""}><option value="">Select driver</option>{drivers.map((item) => <option key={item.siteDriverCode} value={item.siteDriverCode}>{displayName(item)} ({item.siteDriverCode})</option>)}</select></div></div><div className="button-row"><button className="button button-primary" type="submit">Fetch Details</button><Link className="button button-secondary" href="/trips/driver-licence-details">Clear</Link></div></form>{driver === null ? <section className="vehicle-empty-state" aria-live="polite"><p className="eyebrow">No record selected</p><h2>No driver licence details found.</h2><p className="muted-copy">Select a site driver and fetch the details.</p></section> : <DetailTable driver={driver} siteName={site?.description ? `${site.description} (${driver.siteCode})` : String(driver.siteCode)} licenceType={licenceType?.description || licenceType?.code || String(driver.driverLicenceTypeId)} />}<div className="vehicle-footer-actions"><Link className="button button-secondary" href="/trip-authorities">Back to Trips</Link><Link className="button button-secondary" href="/home">Home</Link></div></section></main>;
  } catch (error) {
    if (error instanceof DriverManagementApiError && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/trips/driver-licence-details" /></main>;
    console.error("FIS driver licence details request failed", error instanceof Error ? error.message : "unknown error");
    return <main className="page-shell vehicle-page-shell"><ApiUnavailable /></main>;
  }
}
