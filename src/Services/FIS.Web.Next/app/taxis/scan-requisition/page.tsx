import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { deleteTaxiScanDocAction, uploadTaxiScanDocAction } from "@/app/taxis/actions";
import { dateValue, queryValue, TaxiHeader, TaxiNotice, TaxiRestricted, TaxiUnavailable, valueOrDash } from "@/app/taxis/_components";
import { TaxiApiError, getTaxiScanDocs } from "@/lib/api-taxis";
import { getVehicleOptions, VehicleApiError } from "@/lib/api-vehicles";
import { getSession } from "@/lib/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function vehicleLabel(vehicle: { fleetNumber: string | null; registrationNumber: string | null; vmfCode: number }) {
  return [vehicle.fleetNumber, vehicle.registrationNumber].filter(Boolean).join(" / ") || `Vehicle ${vehicle.vmfCode}`;
}

export default async function TaxiScanRequisitionPage({ searchParams }: Readonly<{ searchParams?: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/taxis/scan-requisition" /></main>;
  if (!session.roles.some((role) => role.localeCompare("Private Hire Vehicles", undefined, { sensitivity: "accent" }) === 0)) return <main className="page-shell vehicle-page-shell"><TaxiRestricted subject="Taxi requisition scanning" /></main>;

  const query = searchParams ? await searchParams : {};
  try {
    const [documents, vehicles] = await Promise.all([getTaxiScanDocs(), getVehicleOptions()]);
    const search = queryValue(query.search).trim().toLowerCase();
    const filteredDocuments = search
      ? documents.filter((document) => [document.fleetNumber, document.registrationNumber, String(document.vmfCode), document.image ?? ""].some((value) => value?.toLowerCase().includes(search)))
      : documents;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="taxi-scan-title">
          <TaxiHeader title="Scan Taxi Requisition" description="Upload, review, or remove scanned requisition certificates." />
          <TaxiNotice query={query} />
          <section className="vehicle-status-maintenance-panel" aria-labelledby="taxi-scan-upload-title">
            <div className="vehicle-form-section-header"><div><p className="eyebrow">Upload certificate</p><h2 id="taxi-scan-upload-title">Add a scanned requisition certificate</h2></div></div>
            <form action={uploadTaxiScanDocAction} encType="multipart/form-data" className="vehicle-form">
              <input type="hidden" name="returnPath" value="/taxis/scan-requisition" />
              <div className="vehicle-form-grid">
                <label className="form-label" htmlFor="taxi-scan-vehicle">GG or GP vehicle<select className="form-input" id="taxi-scan-vehicle" name="vmfCode" required defaultValue=""><option value="" disabled>Select a vehicle</option>{vehicles.map((vehicle) => <option key={vehicle.vmfCode} value={vehicle.vmfCode}>{vehicleLabel(vehicle)}</option>)}</select></label>
                <label className="form-label" htmlFor="taxi-scan-begin">Certificate period from<input className="form-input" id="taxi-scan-begin" name="periodBegin" type="date" required /></label>
                <label className="form-label" htmlFor="taxi-scan-end">Certificate period to<input className="form-input" id="taxi-scan-end" name="periodEnd" type="date" required /></label>
                <label className="form-label" htmlFor="taxi-scan-file">Scanned certificate<input className="form-input" id="taxi-scan-file" name="file" type="file" accept="image/jpeg,image/png,image/gif,image/webp" capture="environment" required /></label>
              </div>
              <p className="muted-copy">Use a JPG, PNG, GIF, or WEBP scan up to 20 MB. The original Taxi_ScanDocs table remains the source of record.</p>
              <button className="button button-primary" type="submit">Upload certificate</button>
            </form>
          </section>
          <section className="vehicle-status-maintenance-panel" aria-labelledby="taxi-scan-list-title">
            <div className="vehicle-form-section-header"><div><p className="eyebrow">Certificate register</p><h2 id="taxi-scan-list-title">Scanned requisition certificates</h2></div><span className="muted-copy">{filteredDocuments.length} record{filteredDocuments.length === 1 ? "" : "s"}</span></div>
            <form method="get" className="vehicle-search-row"><label className="sr-only" htmlFor="taxi-scan-search">Search certificates</label><input className="vehicle-search" id="taxi-scan-search" name="search" defaultValue={queryValue(query.search)} placeholder="Search fleet, registration, or vehicle code" /><button className="button button-secondary" type="submit">Search</button></form>
            {filteredDocuments.length === 0 ? <p className="muted-copy">No scanned requisition certificates found.</p> : <div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Scanned taxi requisition certificates</caption><thead><tr><th scope="col">Fleet number</th><th scope="col">Registration</th><th scope="col">Period from</th><th scope="col">Period to</th><th scope="col">Uploaded</th><th scope="col">Certificate</th><th scope="col"><span className="sr-only">Actions</span></th></tr></thead><tbody>{filteredDocuments.slice(0, 500).map((document) => <tr key={document.scanDocCode}><td>{valueOrDash(document.fleetNumber ?? document.vmfCode)}</td><td>{valueOrDash(document.registrationNumber)}</td><td>{dateValue(document.periodBegin)}</td><td>{dateValue(document.periodEnd)}</td><td>{dateValue(document.dateCreated)}</td><td><Link href={document.fileUrl} target="_blank" rel="noreferrer">{valueOrDash(document.image)}</Link></td><td><form action={deleteTaxiScanDocAction}><input type="hidden" name="returnPath" value="/taxis/scan-requisition" /><input type="hidden" name="scanDocCode" value={document.scanDocCode} /><button className="button button-danger button-sm" type="submit">Delete</button></form></td></tr>)}</tbody></table></div>}
          </section>
          <div className="button-row"><Link className="button button-secondary" href="/taxis">Back to Taxi Menu</Link><Link className="button button-secondary" href="/Taxi_Scan_Docs/RekCert_Menu.aspx">Legacy route menu</Link></div>
        </section>
      </main>
    );
  } catch (error) {
    if ((error instanceof TaxiApiError || error instanceof VehicleApiError) && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/taxis/scan-requisition" /></main>;
    return <main className="page-shell vehicle-page-shell"><TaxiUnavailable path="/taxis/scan-requisition" subject="Taxi requisition scans" /></main>;
  }
}
