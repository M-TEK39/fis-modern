import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import { saveSiteDriverAction } from "@/app/drivers/actions";
import { contextPath, getQueryValue, hasVehicleManagementPermission, parsePositiveInteger } from "@/app/drivers/access";
import {
  DriverManagementApiError,
  getDriverManagementDepartments,
  getDriverManagementLicenceTypes,
  getDriverManagementSiteDriver,
  getDriverManagementSites,
} from "@/lib/api-driver-management";
import { getSession } from "@/lib/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function resultMessage(result: string | undefined, detail: string | undefined) {
  if (result === "success") return { tone: "success", text: "Site-driver change saved successfully." } as const;
  if (result === "invalid") return { tone: "error", text: detail || "Check the site-driver fields and try again." } as const;
  if (result === "forbidden") return { tone: "error", text: "You do not have permission to maintain site drivers." } as const;
  if (result === "not-found") return { tone: "error", text: "The selected site driver could not be found." } as const;
  if (result === "unauthorized") return { tone: "error", text: "Your session is no longer authorized. Sign in again." } as const;
  if (result === "unavailable") return { tone: "error", text: "The site-driver service is unavailable. Retry when the API is available." } as const;
  if (result === "rejected") return { tone: "error", text: "The site-driver change was rejected by the database." } as const;
  return result ? { tone: "error", text: "The site-driver change could not be completed." } as const : null;
}

function dateInputValue(value: string | null | undefined) {
  return value ? value.slice(0, 10) : "";
}

export default async function SiteDriverEditPage({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/drivers/site-drivers/edit" /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>Site-driver details could not be loaded.</h2></section></main>;
  if (!hasVehicleManagementPermission(session.accessLevel)) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to maintain site drivers.</h2><Link className="button button-secondary" href="/drivers">Back</Link></section></main>;

  const query = await searchParams;
  const departmentCode = parsePositiveInteger(getQueryValue(query.departmentCode));
  const siteCode = parsePositiveInteger(getQueryValue(query.siteCode));
  const siteDriverCode = parsePositiveInteger(getQueryValue(query.siteDriverCode));
  if (!departmentCode || !siteCode) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Selection required</p><h2>Select a department and site before editing site drivers.</h2><Link className="button button-secondary" href="/drivers">Back to Driver Management</Link></section></main>;

  try {
    const [departments, sites, licenceTypes, driver] = await Promise.all([
      getDriverManagementDepartments(),
      getDriverManagementSites(),
      getDriverManagementLicenceTypes(),
      siteDriverCode ? getDriverManagementSiteDriver(siteDriverCode) : Promise.resolve(null),
    ]);
    if (siteDriverCode && !driver) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Record not found</p><h2>The selected site driver could not be loaded.</h2><Link className="button button-secondary" href={contextPath("/drivers/site-drivers", departmentCode, siteCode)}>Back to Site Driver Management</Link></section></main>;

    const department = departments.find((item) => item.code === departmentCode);
    const site = sites.find((item) => item.code === siteCode);
    const backPath = contextPath("/drivers/site-drivers", departmentCode, siteCode);
    const message = resultMessage(getQueryValue(query.result), getQueryValue(query.message));
    const title = driver ? "Edit Site Driver" : "Add Site Driver";

    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="site-driver-edit-title">
          <header className="vehicle-page-header"><div><p className="eyebrow">{department?.description ?? `Department ${departmentCode}`} / {site?.description ?? `Site ${siteCode}`}</p><h1 id="site-driver-edit-title">{title}</h1><p>Capture every field from the original SiteDriverEdit workflow.</p></div><Link className="button button-secondary" href={backPath}>Back</Link></header>
          {message ? <div className={`notice notice-${message.tone}`} role={message.tone === "error" ? "alert" : "status"}>{message.text}</div> : null}
          <form action={saveSiteDriverAction} className="vehicle-status-maintenance-panel">
            <input name="departmentCode" type="hidden" value={departmentCode} />
            <input name="siteCode" type="hidden" value={siteCode} />
            {driver ? <input name="siteDriverCode" type="hidden" value={driver.siteDriverCode} /> : null}
            <div className="vehicle-form-section-header"><div><p className="eyebrow">Legacy fields</p><h2>Site driver details</h2><p>Identity and licence validation follows the established workflow.</p></div></div>
            <div className="form-grid">
              <div className="form-field"><label className="form-label" htmlFor="site-driver-firstname">First Name</label><input className="form-input" id="site-driver-firstname" maxLength={50} name="driverFirstname" defaultValue={driver?.driverFirstname ?? ""} required /></div>
              <div className="form-field"><label className="form-label" htmlFor="site-driver-surname">Surname</label><input className="form-input" id="site-driver-surname" maxLength={50} name="driverSurname" defaultValue={driver?.driverSurname ?? ""} required /></div>
              <div className="form-field"><label className="form-label" htmlFor="site-driver-persal">Persal Number</label><input className="form-input" id="site-driver-persal" maxLength={10} name="driverPersonalNumber" defaultValue={driver?.driverPersonalNumber ?? ""} /></div>
              <div className="form-field"><label className="form-label" htmlFor="site-driver-contract">Contract Number</label><input className="form-input" id="site-driver-contract" maxLength={10} name="driverContractNumber" defaultValue={driver?.driverContractNumber ?? ""} /></div>
              <div className="form-field"><label className="form-label" htmlFor="site-driver-sa-id">South African ID</label><input className="form-input" id="site-driver-sa-id" inputMode="numeric" maxLength={13} name="driverSAId" defaultValue={driver?.driverSAId ?? ""} /></div>
              <div className="form-field"><label className="form-label" htmlFor="site-driver-passport">Passport Number</label><input className="form-input" id="site-driver-passport" maxLength={20} name="driverPassportNumber" defaultValue={driver?.driverPassportNumber ?? ""} /></div>
              <div className="form-field"><label className="form-label" htmlFor="site-driver-licence">Licence Number</label><input className="form-input" id="site-driver-licence" maxLength={20} name="driverLicenceNumber" defaultValue={driver?.driverLicenceNumber ?? ""} required /></div>
              <div className="form-field"><label className="form-label" htmlFor="site-driver-issue-date">Licence Issue Date</label><input className="form-input" id="site-driver-issue-date" name="driverLicenceIssueDate" type="date" defaultValue={dateInputValue(driver?.driverLicenceIssueDate)} required /></div>
              <div className="form-field"><label className="form-label" htmlFor="site-driver-verified-date">Licence Last Verified Date</label><input className="form-input" id="site-driver-verified-date" name="driverLicenceLastVerifiedDate" type="date" defaultValue={dateInputValue(driver?.driverLicenceLastVerifiedDate)} required /></div>
              <div className="form-field"><label className="form-label" htmlFor="site-driver-pdp">Driver has PDP</label><label className="vehicle-checkbox-label" htmlFor="site-driver-pdp"><input id="site-driver-pdp" name="driverHasPDP" type="checkbox" value="true" defaultChecked={driver?.driverHasPDP ?? false} /> PDP recorded</label></div>
              <div className="form-field"><label className="form-label" htmlFor="site-driver-pdp-expiry">PDP Expiry Date</label><input className="form-input" id="site-driver-pdp-expiry" name="driverPDPExpiryDate" type="date" defaultValue={dateInputValue(driver?.driverPDPExpiryDate)} /></div>
              <div className="form-field"><label className="form-label" htmlFor="site-driver-licence-expiry">Licence Expiry Date</label><input className="form-input" id="site-driver-licence-expiry" name="driverLicenceExpiryDate" type="date" defaultValue={dateInputValue(driver?.driverLicenceExpiryDate)} /></div>
              <div className="form-field form-group-full"><label className="form-label" htmlFor="site-driver-licence-type">Driver Licence Type</label><select className="form-select" id="site-driver-licence-type" name="driverLicenceTypeId" defaultValue={driver?.driverLicenceTypeId ?? ""} required><option value="">Select driver licence type</option>{licenceTypes.map((licenceType) => <option key={licenceType.id} value={licenceType.id}>{licenceType.description || licenceType.code || `Licence ${licenceType.id}`}</option>)}</select></div>
            </div>
            <div className="button-row"><button className="button button-primary" type="submit">{driver ? "Save Changes" : "Add Site Driver"}</button><Link className="button button-secondary" href={backPath}>Cancel</Link></div>
          </form>
          <div className="vehicle-footer-actions"><Link className="button button-secondary" href="/home">Home</Link><form action={logoutAction}><button className="button button-secondary" type="submit">Sign out</button></form></div>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof DriverManagementApiError && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={contextPath("/drivers/site-drivers/edit", departmentCode, siteCode)} /></main>;
    console.error("FIS site-driver edit request failed", error instanceof Error ? error.message : "unknown error");
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>Site-driver details could not be loaded.</h2><Link className="button button-primary" href={contextPath("/drivers/site-drivers/edit", departmentCode, siteCode)}>Try again</Link></section></main>;
  }
}
