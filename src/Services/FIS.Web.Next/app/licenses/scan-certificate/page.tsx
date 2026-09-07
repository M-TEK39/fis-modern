import Link from "next/link";

import { deleteLicenseCertificateAction, uploadLicenseCertificateAction } from "@/app/licenses/scan-certificate/actions";
import { LicenseMenu, LicenseNotice, LicenseShell, valueOrDash } from "@/app/licenses/_components";
import { accessRestricted, getLicenseSession, hasLicenseAccess, queryValue, sessionMessage } from "@/app/licenses/_page";
import {
  getLicenseCertificates,
  getLicenseCertificatesForVehicle,
  getVehiclesMissingLicenseCertificates,
  LicenseCertificateApiError,
  searchLicenseCertificateVehicles,
  type CertificateVehicle,
  type LicenseCertificateRecord,
} from "@/lib/api-license-certificates";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function certificateFileHref(certificate: LicenseCertificateRecord) {
  return `/licenses/scan-certificate/${certificate.source}/${encodeURIComponent(certificate.documentKey)}/file?vmfCode=${certificate.vmfCode}`;
}

function VehicleSearch({ mode, number, view, vmfCode }: Readonly<{ mode: "GG" | "GP"; number: string; view: string; vmfCode: string }>) {
  return <section className="vehicle-status-maintenance-panel" aria-labelledby="certificate-search-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Certificate lookup</p><h2 id="certificate-search-title">Find a vehicle</h2></div></div><form method="get"><input name="view" type="hidden" value="vehicle" /><input name="lookup" type="hidden" value="1" /><input name="vmfCode" type="hidden" value={vmfCode} /><div className="form-grid"><fieldset className="vehicle-search-options"><legend>Number type</legend><label className="vehicle-checkbox-label"><input name="mode" type="radio" value="GG" defaultChecked={mode === "GG"} /> GG</label><label className="vehicle-checkbox-label"><input name="mode" type="radio" value="GP" defaultChecked={mode === "GP"} /> GP</label></fieldset><div className="form-field"><label className="form-label" htmlFor="certificate-vehicle-number">{mode === "GG" ? "GG Number" : "GP Number"}</label><input className="form-input" id="certificate-vehicle-number" name="number" defaultValue={number} maxLength={50} required /></div></div><div className="button-row"><button className="button button-primary" type="submit">Find vehicle</button><Link className="button button-secondary" href="/licenses/scan-certificate">Clear</Link></div></form></section>;
}

function VehicleMatches({ matches, mode, number }: Readonly<{ matches: readonly CertificateVehicle[]; mode: "GG" | "GP"; number: string }>) {
  if (matches.length === 0) return <p className="muted-copy">No vehicle matches were found for {number}.</p>;
  return <section className="vehicle-status-maintenance-panel" aria-labelledby="certificate-vehicle-results-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Vehicle matches</p><h2 id="certificate-vehicle-results-title">Select a vehicle</h2></div></div><div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Vehicles matching certificate search</caption><thead><tr><th scope="col">GG Number</th><th scope="col">GP Number</th><th scope="col">VMF Code</th><th scope="col">Action</th></tr></thead><tbody>{matches.map((vehicle) => <tr key={vehicle.vmfCode}><td>{valueOrDash(vehicle.fleetNumber)}</td><td>{valueOrDash(vehicle.registrationNumber)}</td><td>{vehicle.vmfCode}</td><td><Link className="button button-secondary button-small" href={`/licenses/scan-certificate?${new URLSearchParams({ view: "vehicle", mode, number, lookup: "1", vmfCode: String(vehicle.vmfCode) }).toString()}`}>Open certificate</Link></td></tr>)}</tbody></table></div></section>;
}

function CertificateTable({ certificates, returnPath }: Readonly<{ certificates: readonly LicenseCertificateRecord[]; returnPath: string }>) {
  if (certificates.length === 0) return <section className="vehicle-empty-state" aria-live="polite"><p className="eyebrow">No certificates found</p><h2>No scanned licence certificates are available.</h2></section>;
  return <section className="vehicle-status-maintenance-panel" aria-labelledby="certificate-results-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Certificate scans</p><h2 id="certificate-results-title">{certificates.length} certificate(s)</h2></div></div><div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Scanned licence certificates</caption><thead><tr><th scope="col">GG Number</th><th scope="col">GP Number</th><th scope="col">Period From</th><th scope="col">Period To</th><th scope="col">File</th><th scope="col">Source</th><th scope="col">Actions</th></tr></thead><tbody>{certificates.map((certificate) => <tr key={`${certificate.source}-${certificate.documentKey}-${certificate.vmfCode}`}><td>{valueOrDash(certificate.fleetNumber)}</td><td>{valueOrDash(certificate.registrationNumber)}</td><td>{formatDate(certificate.periodBegin)}</td><td>{formatDate(certificate.periodEnd)}</td><td><Link href={certificateFileHref(certificate)} target="_blank" rel="noreferrer">{valueOrDash(certificate.fileName ?? certificate.image)}</Link></td><td>{certificate.source}</td><td><form action={deleteLicenseCertificateAction}><input name="source" type="hidden" value={certificate.source} /><input name="documentKey" type="hidden" value={certificate.documentKey} /><input name="vmfCode" type="hidden" value={certificate.vmfCode} /><input name="returnPath" type="hidden" value={returnPath} /><button className="button button-danger button-small" type="submit">Delete</button></form></td></tr>)}</tbody></table></div></section>;
}

function MissingCertificates({ location, vehicles }: Readonly<{ location: string; vehicles: Awaited<ReturnType<typeof getVehiclesMissingLicenseCertificates>>["vehicles"] }>) {
  return <section className="vehicle-status-maintenance-panel" aria-labelledby="missing-certificates-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Active fleet</p><h2 id="missing-certificates-title">Vehicles without scanned certificates</h2></div></div><form method="get" className="form-grid"><input name="view" type="hidden" value="missing" /><div className="form-field"><label className="form-label" htmlFor="certificate-location">Garage</label><select className="form-select" id="certificate-location" name="location" defaultValue={location}><option value="">All garages</option><option value="jhb">Johannesburg</option><option value="pta">Pretoria</option></select></div><div className="button-row"><button className="button button-primary" type="submit">Refresh list</button></div></form>{vehicles.length === 0 ? <p className="muted-copy">Every active vehicle in the selected garage has a scanned certificate.</p> : <div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Vehicles without scanned licence certificates</caption><thead><tr><th scope="col">#</th><th scope="col">GG Number</th><th scope="col">GP Number</th><th scope="col">VMF Code</th><th scope="col">Action</th></tr></thead><tbody>{vehicles.map((vehicle) => <tr key={vehicle.vmfCode}><td>{vehicle.number}</td><td>{valueOrDash(vehicle.fleetNumber)}</td><td>{valueOrDash(vehicle.registrationNumber)}</td><td>{vehicle.vmfCode}</td><td><Link className="button button-secondary button-small" href={`/licenses/scan-certificate?${new URLSearchParams({ view: "vehicle", mode: "GG", number: vehicle.fleetNumber ?? "", lookup: "1", vmfCode: String(vehicle.vmfCode) }).toString()}`}>Open vehicle</Link></td></tr>)}</tbody></table></div>}</section>;
}

function UploadForm({ vmfCode, returnPath }: Readonly<{ vmfCode: number; returnPath: string }>) {
  return <section className="vehicle-status-maintenance-panel" aria-labelledby="certificate-upload-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Vehicle {vmfCode}</p><h2 id="certificate-upload-title">Upload scanned licence certificate</h2></div></div><form action={uploadLicenseCertificateAction} encType="multipart/form-data"><input name="vmfCode" type="hidden" value={vmfCode} /><input name="returnPath" type="hidden" value={returnPath} /><div className="form-grid"><div className="form-field"><label className="form-label" htmlFor="certificate-file">Certificate image</label><input className="form-input" id="certificate-file" name="file" type="file" accept="image/jpeg,image/png,image/gif" capture="environment" required /><span className="form-hint">JPG, PNG, or GIF up to 20 MB.</span></div><div className="form-field"><label className="form-label" htmlFor="certificate-period-begin">Period From</label><input className="form-input" id="certificate-period-begin" name="periodBegin" type="date" /></div><div className="form-field"><label className="form-label" htmlFor="certificate-period-end">Period To</label><input className="form-input" id="certificate-period-end" name="periodEnd" type="date" /></div></div><div className="button-row"><button className="button button-primary" type="submit">Upload certificate</button></div></form></section>;
}

export default async function LicenseScanCertificatePage({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  const session = await getLicenseSession();
  const problem = sessionMessage(session, "/licenses/scan-certificate");
  if (problem) return problem;
  if (session.status !== "authenticated") return accessRestricted("Your session could not be loaded.");
  if (!hasLicenseAccess(session)) return accessRestricted();

  const query = await searchParams;
  const view = queryValue(query.view) || "menu";
  const mode = queryValue(query.mode).toUpperCase() === "GP" ? "GP" : "GG";
  const number = queryValue(query.number).trim();
  const vmfCodeValue = Number(queryValue(query.vmfCode));
  const vmfCode = Number.isSafeInteger(vmfCodeValue) && vmfCodeValue > 0 ? vmfCodeValue : null;
  const lookup = queryValue(query.lookup) === "1";
  const location = queryValue(query.location);
  const returnPath = `/licenses/scan-certificate?${new URLSearchParams({ view, mode, ...(number ? { number } : {}), ...(vmfCode ? { vmfCode: String(vmfCode) } : {}), ...(lookup ? { lookup: "1" } : {}), ...(location ? { location } : {}) }).toString()}`;
  let matches: CertificateVehicle[] = [];
  let certificates: LicenseCertificateRecord[] = [];
  let missing: Awaited<ReturnType<typeof getVehiclesMissingLicenseCertificates>>["vehicles"] = [];
  let loadError = "";
  try {
    if (view === "vehicle" && lookup && number && !vmfCode) matches = await searchLicenseCertificateVehicles(number);
    if (view === "vehicle" && vmfCode) certificates = await getLicenseCertificatesForVehicle(vmfCode);
    if (view === "all") certificates = await getLicenseCertificates();
    if (view === "missing") missing = (await getVehiclesMissingLicenseCertificates(location === "jhb" || location === "pta" ? location : undefined)).vehicles;
  } catch (error) {
    loadError = error instanceof LicenseCertificateApiError && error.reason === "not-found" ? "The selected vehicle or certificate was not found." : "Certificate data could not be loaded. Retry when the FIS API is available.";
    console.error("FIS licence certificate request failed", error instanceof Error ? error.message : "unknown error");
  }

  return <LicenseShell title="Scan Licence Certificate" description="Upload, view, and remove scanned licence certificates."><LicenseNotice query={query} />{loadError ? <section className="vehicle-status-card" role="alert"><h2>{loadError}</h2></section> : null}<div className="button-row"><Link className="button button-secondary" href="/licenses/scan-certificate?view=all">List all certificates</Link><Link className="button button-secondary" href="/licenses/scan-certificate?view=missing">Vehicles without certificates</Link></div>{view === "menu" ? <><section className="vehicle-status-maintenance-panel"><p>Search by GG or GP number to upload or review a vehicle certificate.</p></section><LicenseMenu /></> : null}{view === "vehicle" ? <><VehicleSearch mode={mode} number={number} view={view} vmfCode={vmfCode ? String(vmfCode) : ""} />{lookup && number && !vmfCode ? <VehicleMatches matches={matches} mode={mode} number={number} /> : null}{vmfCode ? <><UploadForm vmfCode={vmfCode} returnPath={returnPath} /><CertificateTable certificates={certificates} returnPath={returnPath} /></> : null}</> : null}{view === "all" ? <CertificateTable certificates={certificates} returnPath={returnPath} /> : null}{view === "missing" ? <MissingCertificates location={location} vehicles={missing} /> : null}<div className="vehicle-footer-actions"><Link className="button button-secondary" href="/licenses">Licence Menu</Link><Link className="button button-secondary" href="/home">Home</Link></div></LicenseShell>;
}
