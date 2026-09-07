import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { hasAssetVerificationAccess, getQueryValue } from "@/app/vehicle-verification/access";
import { saveAssetVerificationAction } from "@/app/vehicle-verification/actions";
import { AssetVerificationApiError, getAssetVerifications, type AssetVerificationRecord } from "@/lib/api-asset-verification";
import { ContractApiError, getContractPage } from "@/lib/api-contracts";
import { DepartmentApiError, getDepartment } from "@/lib/api-departments";
import { SiteApiError, getSite } from "@/lib/api-sites";
import { getVehicleForEdit, VehicleEditApiError } from "@/lib/api-vehicle-edit";
import { searchVehiclesAgainstApi, type VehicleSearchResult, VehicleCreateApiError } from "@/lib/api-vehicle-create";
import { getSession } from "@/lib/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type Mode = "add" | "edit";

const PROVINCES = [
  ["Eatern Cape", "Eastern Cape"], ["Free State", "Free State"], ["Gauteng", "Gauteng"], ["Kwazulu-Namtal", "Kwazulu-Natal"],
  ["Limpopo", "Limpopo"], ["Mpumalanga", "Mpumalanga"], ["Northern Cape", "Northern Cape"], ["North West", "North West"], ["Western Cape", "Western Cape"],
] as const;
const YES_NO = ["Select...", "Yes", "No"] as const;

function dateInput(value: string | null | undefined) { return value ? value.slice(0, 10) : ""; }
function valueOrDash(value: string | number | null | undefined) { return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value); }
function exactVehicle(matches: VehicleSearchResult[], query: string) {
  const term = query.trim().toLocaleLowerCase();
  return matches.filter((vehicle) => [vehicle.fleetNumber, vehicle.registrationNumber].some((value) => value?.trim().toLocaleLowerCase() === term));
}
function recordMatches(record: AssetVerificationRecord, vehicle: VehicleSearchResult, query: string) {
  const term = query.trim().toLocaleLowerCase();
  return record.vmfCode === vehicle.vmfCode || record.vehicleRegNo?.trim().toLocaleLowerCase() === term || record.vehicleRegNo?.trim().toLocaleLowerCase() === vehicle.registrationNumber?.trim().toLocaleLowerCase();
}
function ErrorCard({ title, message, backHref }: Readonly<{ title: string; message: string; backHref: string }>) {
  return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Asset verification</p><h1>{title}</h1><p className="muted-copy">{message}</p><Link className="button button-secondary" href={backHref}>Back</Link></section></main>;
}

export async function AssetVerificationDetailsPage({ mode, searchParams, routePath }: Readonly<{ mode: Mode; searchParams: SearchParams; routePath: string }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
  if (session.status === "unavailable") return <ErrorCard title="Vehicle lookup is unavailable." message="Retry when the FIS API is available." backHref={`/vehicle-verification/${mode}`} />;
  if (!hasAssetVerificationAccess(session.roles, session.accessLevel)) return <ErrorCard title="Access restricted" message="You do not have permission to maintain asset verification." backHref="/vehicle-verification" />;

  const query = await searchParams;
  const gg = (getQueryValue(query.gg) ?? getQueryValue(query.txtGGNum) ?? "").trim().slice(0, 30);
  if (!gg) return <ErrorCard title="Vehicle is not selected." message="Search for a GG or registration number before opening asset verification details." backHref={`/vehicle-verification/${mode}`} />;

  try {
    const matches = await searchVehiclesAgainstApi(gg);
    const exactMatches = exactVehicle(matches, gg);
    if (exactMatches.length !== 1) return <ErrorCard title={exactMatches.length === 0 ? "Vehicle not found." : "Vehicle selection is ambiguous."} message={exactMatches.length === 0 ? `No exact GG or registration match was found for “${gg}”.` : "More than one vehicle has the same identifier. Search again using an exact identifier."} backHref={`/vehicle-verification/${mode}?q=${encodeURIComponent(gg)}`} />;
    const vehicle = await getVehicleForEdit(exactMatches[0].vmfCode);
    const [records, contracts] = await Promise.all([
      getAssetVerifications(),
      getContractPage({ vmfCode: vehicle.vmfCode, page: 1, pageSize: 100 }),
    ]);
    const existing = records.find((record) => recordMatches(record, exactMatches[0], gg));
    if (mode === "add" && existing) return <ErrorCard title="Asset verification already exists." message="This vehicle has already been added. Use the Edit menu option to change it." backHref="/vehicle-verification" />;
    if (mode === "edit" && !existing) return <ErrorCard title="Asset verification record not found." message="Use the Add menu option to create the vehicle's first verification record." backHref="/vehicle-verification/edit" />;

    const contract = contracts.items.find((item) => item.stillCurrent?.toUpperCase() === "Y") ?? contracts.items[0];
    const siteCode = existing?.siteCode ?? contract?.siteCode ?? null;
    if (!siteCode) return <ErrorCard title="No contract site found." message={`No site could be found for vehicle “${gg}”. Open or correct its contract before capturing asset verification.`} backHref={`/vehicle-verification/${mode}`} />;
    const site = siteCode ? await getSite(siteCode) : null;
    const department = site?.departmentCode ? await getDepartment(site.departmentCode) : null;
    const record = existing;
    const initial = {
      province: record?.province || "Select...",
      departmentName: record?.departmentName ?? department?.description ?? "",
      siteName: record?.siteName ?? site?.description ?? "",
      siteCode: siteCode ? String(siteCode) : "",
      responsibleManager: record?.responsibleManager ?? site?.responsiblePerson ?? "",
      telNo: record?.telNo ?? site?.telephone ?? "",
      faxNo: record?.faxNo ?? site?.fax ?? "",
      vehicleMake: record?.vehicleMake ?? "",
      vehicleModel: record?.vehicleModel ?? vehicle.modelName ?? "",
      vehicleColour: record?.vehicleColour ?? vehicle.colour ?? "",
      mobitrackFitted: record?.mobitrackFitted ?? "Select...",
      petrolCard: record?.petrolCard ?? "Select...",
      lamination: record?.lamination ?? "Select...",
      tyreBands: record?.tyreBands ?? "Select...",
      barcode: record?.barcode ?? "Select...",
      logbook: record?.logbook ?? "Select...",
      gearlock: record?.gearlock ?? "Select...",
      radio: record?.radio ?? "Select...",
      carKeys: record?.carKeys ?? "Select...",
      licenceExpiryDate: dateInput(record?.licenceExpiryDate ?? vehicle.licenceDueDate),
      barcodeNumber: record?.barcodeNumber ?? "",
      vehicleEngineNumber: record?.vehicleEngineNumber ?? vehicle.engineNumber,
      vehicleChassisNumber: record?.vehicleChassisNumber ?? vehicle.chassisNumber,
      currentKm: String(record?.currentKm ?? vehicle.currentOdo ?? ""),
      lastVerified: dateInput(record?.dateLastVerified ?? record?.verificationDate),
      comments: record?.comments ?? record?.notes ?? "",
    };
    const result = getQueryValue(query.result);
    const message = getQueryValue(query.message);
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="asset-verification-details-title"><header className="vehicle-page-header"><div><p className="eyebrow">Vehicle asset verification</p><h1 id="asset-verification-details-title">{mode === "add" ? "Add" : "Edit"} Asset Verification Details</h1><p>{valueOrDash(vehicle.fleetNumber)} / {valueOrDash(vehicle.registrationNumber)} ({vehicle.vmfCode})</p></div><Link className="button button-secondary" href="/vehicle-verification">Menu</Link></header>{result === "success" ? <div className="notice notice-success" role="status">Asset verification saved successfully.</div> : null}{result === "invalid" || result === "unavailable" || result === "forbidden" ? <div className="notice notice-error" role="alert">{message ?? (result === "forbidden" ? "You do not have permission to save asset verification." : result === "unavailable" ? "The asset verification service is unavailable." : "Check the fields and try again.")}</div> : null}<form action={saveAssetVerificationAction} className="vehicle-status-maintenance-panel"><input name="mode" type="hidden" value={mode} /><input name="gg" type="hidden" value={gg} /><input name="assetVerificationCode" type="hidden" value={record?.assetVerificationCode ?? ""} /><input name="vmfCode" type="hidden" value={vehicle.vmfCode} /><input name="vehicleRegNo" type="hidden" value={vehicle.registrationNumber ?? record?.vehicleRegNo ?? ""} /><input name="siteCode" type="hidden" value={initial.siteCode} /><input name="siteName" type="hidden" value={initial.siteName} /><input name="departmentName" type="hidden" value={initial.departmentName} /><input name="vehicleMake" type="hidden" value={initial.vehicleMake} /><input name="vehicleModel" type="hidden" value={initial.vehicleModel} /><input name="vehicleColour" type="hidden" value={initial.vehicleColour} /><input name="licenceExpiryDate" type="hidden" value={initial.licenceExpiryDate} /><input name="vehicleEngineNumber" type="hidden" value={initial.vehicleEngineNumber} /><input name="vehicleChassisNumber" type="hidden" value={initial.vehicleChassisNumber} /><div className="vehicle-form-section-header"><div><p className="eyebrow">Vehicle context</p><h2>Read-only vehicle and contract details</h2></div></div><div className="form-grid"><div className="form-field"><label className="form-label" htmlFor="asset-verification-department">Department Name</label><input className="form-input" id="asset-verification-department" readOnly value={initial.departmentName} /></div><div className="form-field"><label className="form-label" htmlFor="asset-verification-site">Site Name</label><input className="form-input" id="asset-verification-site" readOnly value={initial.siteName} /></div><div className="form-field"><label className="form-label" htmlFor="asset-verification-reg">Vehicle Reg. No</label><input className="form-input" id="asset-verification-reg" readOnly value={vehicle.registrationNumber ?? ""} /></div><div className="form-field"><label className="form-label" htmlFor="asset-verification-gg">GG No</label><input className="form-input" id="asset-verification-gg" readOnly value={vehicle.fleetNumber ?? ""} /></div><div className="form-field"><label className="form-label" htmlFor="asset-verification-model">Vehicle Model</label><input className="form-input" id="asset-verification-model" readOnly value={initial.vehicleModel} /></div><div className="form-field"><label className="form-label" htmlFor="asset-verification-colour">Vehicle Colour</label><input className="form-input" id="asset-verification-colour" readOnly value={initial.vehicleColour} /></div><div className="form-field"><label className="form-label" htmlFor="asset-verification-engine">Engine Number</label><input className="form-input" id="asset-verification-engine" readOnly value={initial.vehicleEngineNumber} /></div><div className="form-field"><label className="form-label" htmlFor="asset-verification-chassis">Chassis Number</label><input className="form-input" id="asset-verification-chassis" readOnly value={initial.vehicleChassisNumber} /></div></div><div className="vehicle-form-section-header"><div><p className="eyebrow">Verification fields</p><h2>Asset verification details</h2><p>Every field from the original maintenance workflow is retained.</p></div></div><div className="form-grid"><div className="form-field"><label className="form-label" htmlFor="asset-verification-province">Province <span className="required">*</span></label><select className="form-select" id="asset-verification-province" name="province" defaultValue={initial.province} required><option value="Select...">Select...</option>{PROVINCES.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></div><div className="form-field"><label className="form-label" htmlFor="asset-verification-manager">Responsible Manager <span className="required">*</span></label><input className="form-input" id="asset-verification-manager" name="responsibleManager" defaultValue={initial.responsibleManager} maxLength={100} required /></div><div className="form-field"><label className="form-label" htmlFor="asset-verification-tel">Tel. No <span className="required">*</span></label><input className="form-input" id="asset-verification-tel" name="telNo" defaultValue={initial.telNo} maxLength={50} required /></div><div className="form-field"><label className="form-label" htmlFor="asset-verification-fax">Fax No <span className="required">*</span></label><input className="form-input" id="asset-verification-fax" name="faxNo" defaultValue={initial.faxNo} maxLength={50} required /></div>{(["mobitrackFitted", "petrolCard", "lamination", "tyreBands", "barcode", "logbook", "gearlock", "radio", "carKeys"] as const).map((field) => <div className="form-field" key={field}><label className="form-label" htmlFor={`asset-verification-${field}`}>{field === "mobitrackFitted" ? "Mobitrack Fitted" : field === "tyreBands" ? "Tyre Bands" : field === "carKeys" ? "Car Keys" : field[0].toUpperCase() + field.slice(1)} <span className="required">*</span></label><select className="form-select" id={`asset-verification-${field}`} name={field} defaultValue={initial[field]} required>{YES_NO.map((option) => <option key={option} value={option}>{option}</option>)}</select></div>)}<div className="form-field"><label className="form-label" htmlFor="asset-verification-barcode-number">Barcode Number</label><input className="form-input" id="asset-verification-barcode-number" name="barcodeNumber" defaultValue={initial.barcodeNumber} maxLength={100} /></div><div className="form-field"><label className="form-label" htmlFor="asset-verification-current-km">Current KM (from vehicle) <span className="required">*</span></label><input className="form-input" id="asset-verification-current-km" name="currentKm" defaultValue={initial.currentKm} inputMode="numeric" required /></div><div className="form-field"><label className="form-label" htmlFor="asset-verification-last-verified">Last Verified <span className="required">*</span></label><input className="form-input" id="asset-verification-last-verified" name="lastVerified" type="date" defaultValue={initial.lastVerified} required /></div><div className="form-field form-group-full"><label className="form-label" htmlFor="asset-verification-comments">Comments <span className="required">*</span></label><textarea className="form-input" id="asset-verification-comments" name="comments" defaultValue={initial.comments} rows={4} maxLength={2000} required /></div></div><div className="button-row"><button className="button button-primary" type="submit">{mode === "add" ? "Save" : "Save Changes"}</button><Link className="button button-secondary" href="/vehicle-verification">Menu</Link></div></form></section></main>;
  } catch (error) {
    if (error instanceof AssetVerificationApiError || error instanceof ContractApiError || error instanceof DepartmentApiError || error instanceof SiteApiError || error instanceof VehicleEditApiError || error instanceof VehicleCreateApiError) {
      if (error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
    }
    console.error("FIS asset verification details request failed", error instanceof Error ? error.message : "unknown error");
    return <ErrorCard title="Asset verification details are unavailable." message="The vehicle or compatible Asset_Verification data could not be loaded. Retry when the FIS API is available." backHref={`/vehicle-verification/${mode}`} />;
  }
}
