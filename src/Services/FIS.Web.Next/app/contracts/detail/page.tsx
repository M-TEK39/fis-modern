import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { editContractAction, hireContractAction, runContractAction } from "@/app/contracts/actions";
import SessionRecovery from "@/app/home/session-recovery";
import { ContractApiError, getContract, searchContractVehicles, type ContractRecord, type ContractVehicleSearchResult } from "@/lib/api-contracts";
import { getSession } from "@/lib/session";

const CONTRACT_PERMISSION = BigInt(2);

export type ContractDetailPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasContractAccess(accessLevel: string | undefined, roles: readonly string[]) {
  if (roles.some((role) => ["contracts", "contract", "admin", "administrator"].includes(role.trim().toLowerCase()))) return true;
  try {
    return accessLevel ? (BigInt(accessLevel) & CONTRACT_PERMISSION) === CONTRACT_PERMISSION : false;
  } catch {
    return false;
  }
}

function hasApproverAccess(accessLevel: string | undefined, roles: readonly string[]) {
  return roles.some((role) => ["contracts approver", "contracts_approver", "back dating contract (approver)", "admin", "administrator"].includes(role.trim().toLowerCase()));
}

function hasLoadAndManageAccess(roles: readonly string[]) {
  return roles.some((role) => [
    "contract (load and manage)",
    "contracts (load and manage)",
    "contract_load_and_manage",
    "contracts_load_and_manage",
    "admin",
    "administrator",
  ].includes(role.trim().toLowerCase()));
}

function hasCancelAndCloseAccess(roles: readonly string[]) {
  return roles.some((role) => [
    "contract (cancel and close)",
    "contracts (cancel and close)",
    "contract_cancel_and_close",
    "contracts_cancel_and_close",
    "admin",
    "administrator",
  ].includes(role.trim().toLowerCase()));
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function dateInputValue(value: string | null, fallback: string) {
  return value?.slice(0, 10) || fallback;
}

function statusLabel(contract: ContractRecord) {
  switch (contract.contractStatusCode) {
    case 0: return "Draft";
    case 1: return "Pending Review";
    case 2: return "Approved";
    case 3: return "Active";
    case 4: return "Declined for Correction";
    case 5: return "Declined";
    case 6: return "Cancelled";
    case 7: return "Closed";
    default: return contract.stillCurrent?.toUpperCase() === "Y" ? "Active" : "Unknown";
  }
}

function DetailActions({ contract, canApprove, canManage, canCancelClose }: Readonly<{ contract: ContractRecord; canApprove: boolean; canManage: boolean; canCancelClose: boolean }>) {
  const returnPath = `/contracts/detail?contractId=${contract.contractCode}`;
  const status = contract.contractStatusCode;
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="contract-actions-title">
      <p className="eyebrow">Available actions</p>
      <h2 id="contract-actions-title">{statusLabel(contract)}</h2>
      <div className="button-row">
        {status === 0 || status === 4 ? <Link className="button button-primary" href={`${returnPath}#edit-contract`}>Edit contract</Link> : null}
        {status === 0 || status === 4 ? <form action={runContractAction}><input name="contractId" type="hidden" value={contract.contractCode} /><input name="returnPath" type="hidden" value={returnPath} /><input name="action" type="hidden" value="submit" /><button className="button button-secondary" type="submit">Submit for approval</button></form> : null}
        {status === 1 && (contract.createdByUserCode || contract.userCode) ? <form action={runContractAction}><input name="contractId" type="hidden" value={contract.contractCode} /><input name="returnPath" type="hidden" value={returnPath} /><input name="action" type="hidden" value="recall" /><button className="button button-secondary" type="submit">Recall to draft</button></form> : null}
        {status === 1 && canApprove ? <form action={runContractAction}><input name="contractId" type="hidden" value={contract.contractCode} /><input name="returnPath" type="hidden" value={returnPath} /><input name="action" type="hidden" value="approve-activate" /><button className="button button-primary" type="submit">Approve and activate</button></form> : null}
        {status === 2 && canApprove ? <form action={runContractAction}><input name="contractId" type="hidden" value={contract.contractCode} /><input name="returnPath" type="hidden" value={returnPath} /><input name="action" type="hidden" value="approve-activate" /><button className="button button-primary" type="submit">Activate approved contract</button></form> : null}
        {status === 3 && canManage ? <Link className="button button-secondary" href={`${returnPath}#extend-contract`}>Extend</Link> : null}
        {status === 3 && canManage ? <Link className="button button-secondary" href={`${returnPath}#reassign-contract`}>Reassign contract</Link> : null}
        {status === 3 && canCancelClose ? <Link className="button button-secondary" href={`${returnPath}#close-contract`}>Close and return home</Link> : null}
        {status === 3 && canCancelClose ? <form action={runContractAction}><input name="contractId" type="hidden" value={contract.contractCode} /><input name="returnPath" type="hidden" value={returnPath} /><input name="action" type="hidden" value="cancel" /><button className="button button-danger" type="submit">Cancel</button></form> : null}
        {status === 3 && canManage && contract.reliefVehicleOption === true ? <Link className="button button-secondary" href={`/contracts/relief-vehicle-search?contractId=${contract.contractCode}`}>Create relief contract</Link> : null}
      </div>
      {(status === 1 || status === 2) && canApprove ? <form action={runContractAction} className="form-grid contract-action-form"><input name="contractId" type="hidden" value={contract.contractCode} /><input name="returnPath" type="hidden" value={returnPath} /><label className="form-field form-group-full"><span className="form-label">Approval notes</span><textarea className="form-textarea" name="approvalNotes" rows={2} /></label><div className="button-row"><button className="button button-secondary" name="action" value="approve" type="submit">Approve only</button><button className="button button-warning" name="action" value="decline-correction" type="submit">Return for correction</button><button className="button button-danger" name="action" value="decline" type="submit">Decline</button></div></form> : null}
    </section>
  );
}

function ContractFacts({ contract }: Readonly<{ contract: ContractRecord }>) {
  const facts: Array<[string, string]> = [
    ["Contract code", valueOrDash(contract.contractCode)],
    ["Vehicle / GG code", valueOrDash(contract.vmfCode)],
    ["Registration", valueOrDash(contract.registrationNumber)],
    ["Fleet / GG number", valueOrDash(contract.fleetNumber)],
    ["Site", `${valueOrDash(contract.siteDescription)} (${contract.siteCode})`],
    ["Contract type", valueOrDash(contract.contractTypeCode)],
    ["Still current", valueOrDash(contract.stillCurrent)],
    ["Start date", formatDate(contract.startDate)],
    ["Start time", valueOrDash(contract.startTime)],
    ["End date", formatDate(contract.endDate)],
    ["End time", valueOrDash(contract.endTime)],
    ["Start odometer", valueOrDash(contract.startOdometer)],
    ["End odometer", valueOrDash(contract.endOdometer)],
    ["Target return", formatDate(contract.targetReturnDate)],
    ["Driver ID", valueOrDash(contract.driverId)],
    ["Driver name", valueOrDash(contract.driverName)],
    ["Site driver code", valueOrDash(contract.siteDriverCode)],
    ["Authorisation", valueOrDash(contract.authorisation)],
    ["Notes", valueOrDash(contract.notes)],
    ["User code", valueOrDash(contract.userCode)],
    ["Charged until", formatDate(contract.chargedUntil)],
    ["Monthly km", valueOrDash(contract.monthlyKm)],
    ["Hours used", valueOrDash(contract.hoursUsed)],
    ["BAS fund code", valueOrDash(contract.basFundCode)],
    ["BAS objective code", valueOrDash(contract.basObjectiveCode)],
    ["BAS project number", valueOrDash(contract.basProjectNumber)],
    ["BAS responsibility code", valueOrDash(contract.basResponsibilityCode)],
    ["Relief for contract", valueOrDash(contract.reliefForContract)],
    ["Parent contract", valueOrDash(contract.parentContractCode)],
    ["Contract group", valueOrDash(contract.contractGroupCode)],
    ["Vehicle assessment", valueOrDash(contract.vehicleAssessmentCode)],
    ["Journal detail code", valueOrDash(contract.journalDetailCode)],
    ["Locked for transfer", contract.lockedForTransfer ? "Yes" : "No"],
    ["Collector", [contract.collectorFirstname, contract.collectorSurname].filter(Boolean).join(" ") || "-"],
    ["Collector SA ID", valueOrDash(contract.collectorSaId)],
    ["Collector passport", valueOrDash(contract.collectorPassportNumber)],
    ["Collector office number", valueOrDash(contract.collectorOfficeNumber)],
    ["Collector cellphone", valueOrDash(contract.collectorCellphoneNumber)],
    ["Collector office", valueOrDash(contract.collectorOffice)],
    ["Collector designation", valueOrDash(contract.collectorDesignation)],
    ["Relief vehicle option", contract.reliefVehicleOption === null ? "-" : contract.reliefVehicleOption ? "Yes" : "No"],
    ["Lease contract period", valueOrDash(contract.leaseContractPeriod)],
    ["Estimated overall km", valueOrDash(contract.contractEstimatedOverallKm)],
    ["Intended start date", formatDate(contract.intendedStartDate)],
    ["Intended start time", valueOrDash(contract.intendedStartTime)],
    ["Capture date", formatDate(contract.captureDate)],
    ["Modified date", formatDate(contract.modifiedDate)],
    ["Reassigned from", valueOrDash(contract.reassignedFromContractCode)],
    ["Created by", valueOrDash(contract.createdByUserCode)],
    ["Created date", formatDate(contract.dateCreated)],
    ["Modified by", valueOrDash(contract.modifiedByUserCode)],
    ["Updated date", formatDate(contract.dateUpdated)],
  ];
  return <section className="vehicle-status-maintenance-panel" aria-labelledby="contract-facts-title"><p className="eyebrow">Legacy contract record</p><h2 id="contract-facts-title">Contract details</h2><dl className="contract-facts">{facts.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl></section>;
}

function HireForm({ vehicle }: Readonly<{ vehicle: ContractVehicleSearchResult }>) {
  return <form action={hireContractAction} className="vehicle-status-maintenance-panel"><input name="returnPath" type="hidden" value="/contracts/maintenance" /><input name="vmfCode" type="hidden" value={vehicle.vmfCode} /><div className="vehicle-form-section-header"><div><p className="eyebrow">New contract</p><h2>Open contract for {valueOrDash(vehicle.fleetNumber)}</h2><p>{valueOrDash(vehicle.registrationNumber)} ({vehicle.vmfCode})</p></div></div><div className="form-grid"><div className="form-field"><label className="form-label" htmlFor="new-contract-site">Site code</label><input className="form-input" id="new-contract-site" min="1" name="siteCode" type="number" required /></div><div className="form-field"><label className="form-label" htmlFor="new-contract-odo">Start odometer</label><input className="form-input" id="new-contract-odo" min="0" name="startOdometer" type="number" /></div><div className="form-field"><label className="form-label" htmlFor="new-contract-driver">Driver ID</label><input className="form-input" id="new-contract-driver" maxLength={60} name="driverId" /></div><div className="form-field"><label className="form-label" htmlFor="new-contract-site-driver">Site driver code</label><input className="form-input" id="new-contract-site-driver" min="1" name="siteDriverCode" type="number" /></div><div className="form-field"><label className="form-label" htmlFor="new-contract-return">Target return date</label><input className="form-input" id="new-contract-return" name="targetReturnDate" type="date" /></div><div className="form-field"><label className="form-label" htmlFor="new-contract-auth">Authorisation</label><input className="form-input" id="new-contract-auth" maxLength={60} name="authorisation" /></div><div className="form-field form-group-full"><label className="form-label" htmlFor="new-contract-notes">Notes</label><textarea className="form-textarea" id="new-contract-notes" maxLength={1000} name="notes" rows={4} /></div></div><div className="button-row"><button className="button button-primary" type="submit">Open contract</button><Link className="button button-secondary" href="/contracts/maintenance">Cancel</Link></div></form>;
}

function EditForm({ contract, today }: Readonly<{ contract: ContractRecord; today: string }>) {
  return <form action={editContractAction} className="vehicle-status-maintenance-panel" id="edit-contract"><input name="contractId" type="hidden" value={contract.contractCode} /><input name="returnPath" type="hidden" value={`/contracts/detail?contractId=${contract.contractCode}`} /><div className="vehicle-form-section-header"><div><p className="eyebrow">Editable legacy fields</p><h2>Edit contract {contract.contractCode}</h2><p>Only draft and correction-request contracts can be edited.</p></div></div><div className="form-grid"><div className="form-field"><label className="form-label" htmlFor="edit-contract-site">Site code</label><input className="form-input" id="edit-contract-site" min="1" name="siteCode" type="number" defaultValue={contract.siteCode} required /></div><div className="form-field"><label className="form-label" htmlFor="edit-contract-odo">Start odometer</label><input className="form-input" id="edit-contract-odo" min="0" name="startOdometer" type="number" defaultValue={contract.startOdometer ?? ""} /></div><div className="form-field"><label className="form-label" htmlFor="edit-contract-driver">Driver ID</label><input className="form-input" id="edit-contract-driver" maxLength={60} name="driverId" defaultValue={contract.driverId ?? ""} /></div><div className="form-field"><label className="form-label" htmlFor="edit-contract-site-driver">Site driver code</label><input className="form-input" id="edit-contract-site-driver" min="1" name="siteDriverCode" type="number" /></div><div className="form-field"><label className="form-label" htmlFor="edit-contract-return">Target return date</label><input className="form-input" id="edit-contract-return" name="targetReturnDate" type="date" defaultValue={dateInputValue(contract.targetReturnDate, today)} /></div><div className="form-field"><label className="form-label" htmlFor="edit-contract-auth">Authorisation</label><input className="form-input" id="edit-contract-auth" maxLength={60} name="authorisation" defaultValue={contract.authorisation ?? ""} /></div><div className="form-field form-group-full"><label className="form-label" htmlFor="edit-contract-notes">Notes</label><textarea className="form-textarea" id="edit-contract-notes" maxLength={1000} name="notes" rows={4} defaultValue={contract.notes ?? ""} /></div></div><div className="button-row"><button className="button button-primary" type="submit">Save contract</button><Link className="button button-secondary" href={`/contracts/detail?contractId=${contract.contractCode}`}>Cancel</Link></div></form>;
}

function ExtendForm({ contract, today }: Readonly<{ contract: ContractRecord; today: string }>) {
  return <form action={runContractAction} className="vehicle-status-maintenance-panel" id="extend-contract"><input name="contractId" type="hidden" value={contract.contractCode} /><input name="returnPath" type="hidden" value={`/contracts/detail?contractId=${contract.contractCode}`} /><input name="action" type="hidden" value="extend" /><div className="form-field"><label className="form-label" htmlFor="new-target-return">New target return date</label><input className="form-input" id="new-target-return" name="newTargetReturnDate" type="date" defaultValue={dateInputValue(contract.targetReturnDate, today)} required /></div><div className="button-row"><button className="button button-primary" type="submit">Save extension</button><Link className="button button-secondary" href={`/contracts/detail?contractId=${contract.contractCode}`}>Cancel</Link></div></form>;
}

function ReassignForm({ contract, today }: Readonly<{ contract: ContractRecord; today: string }>) {
  return <form action={runContractAction} className="vehicle-status-maintenance-panel" id="reassign-contract"><input name="contractId" type="hidden" value={contract.contractCode} /><input name="returnPath" type="hidden" value={`/contracts/detail?contractId=${contract.contractCode}`} /><input name="action" type="hidden" value="reassign" /><div className="vehicle-form-section-header"><div><p className="eyebrow">Reassign active contract</p><h2>Move contract to another site</h2><p>The legacy workflow creates a new effective contract record and closes the previous active record.</p></div></div><div className="form-grid"><div className="form-field"><label className="form-label" htmlFor="reassign-site">Destination site code</label><input className="form-input" id="reassign-site" min="1" name="newSiteCode" type="number" required /></div><div className="form-field"><label className="form-label" htmlFor="reassign-start-date">Effective start date</label><input className="form-input" id="reassign-start-date" name="reassignStartDate" type="date" defaultValue={today} required /></div><div className="form-field"><label className="form-label" htmlFor="reassign-start-odo">Start odometer</label><input className="form-input" id="reassign-start-odo" min="0" name="reassignStartOdometer" type="number" defaultValue={contract.startOdometer ?? ""} required /></div><div className="form-field form-group-full"><label className="form-label" htmlFor="reassign-reason">Reason</label><textarea className="form-textarea" id="reassign-reason" maxLength={1000} name="reassignReason" rows={3} defaultValue="Reassigned from contract detail." /></div></div><div className="button-row"><button className="button button-primary" type="submit">Save reassignment</button><Link className="button button-secondary" href={`/contracts/detail?contractId=${contract.contractCode}`}>Cancel</Link></div></form>;
}

function CloseForm({ contract, today }: Readonly<{ contract: ContractRecord; today: string }>) {
  return <form action={runContractAction} className="vehicle-status-maintenance-panel" id="close-contract"><input name="contractId" type="hidden" value={contract.contractCode} /><input name="returnPath" type="hidden" value={`/contracts/detail?contractId=${contract.contractCode}`} /><input name="action" type="hidden" value="close" /><div className="vehicle-form-section-header"><div><p className="eyebrow">Close active contract</p><h2>Return vehicle home</h2><p>The home department and site are required by the established closing workflow.</p></div></div><div className="form-grid"><div className="form-field"><label className="form-label" htmlFor="close-end-date">Close date</label><input className="form-input" id="close-end-date" name="endDate" type="date" defaultValue={today} required /></div><div className="form-field"><label className="form-label" htmlFor="close-end-odo">Close odometer</label><input className="form-input" id="close-end-odo" min="0" name="endOdometer" type="number" defaultValue={contract.endOdometer ?? ""} required /></div><div className="form-field"><label className="form-label" htmlFor="home-department">Home department code</label><input className="form-input" id="home-department" min="1" name="homeDepartmentCode" type="number" required /></div><div className="form-field"><label className="form-label" htmlFor="home-site">Home site code</label><input className="form-input" id="home-site" min="1" name="homeSiteCode" type="number" required /></div><div className="form-field"><label className="form-label" htmlFor="home-driver">Home site driver code</label><input className="form-input" id="home-driver" min="1" name="homeSiteDriverCode" type="number" /></div><div className="form-field form-group-full"><label className="form-label" htmlFor="close-notes">Close notes</label><textarea className="form-textarea" id="close-notes" maxLength={1000} name="closeNotes" rows={3} /></div><div className="form-field form-group-full"><label className="vehicle-checkbox-label"><input name="createHomeCustodyContract" type="checkbox" value="true" defaultChecked /> Create home custody contract</label></div></div><div className="button-row"><button className="button button-primary" type="submit">Close and return home</button><Link className="button button-secondary" href={`/contracts/detail?contractId=${contract.contractCode}`}>Cancel</Link></div></form>;
}

function ApiUnavailable() {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>Contract details could not be loaded.</h2><p className="muted-copy">The application is still running. Retry when the FIS API is available.</p><Link className="button button-primary" href="/contracts/maintenance">Back to Contracts</Link></section>;
}

export default async function ContractDetailPage({ searchParams, routePath = "/contracts/detail" }: ContractDetailPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><ApiUnavailable /></main>;
  if (!hasContractAccess(session.accessLevel, session.roles)) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to maintain vehicle contracts.</h2></section></main>;

  const query = await searchParams;
  const contractId = positiveInt(getQueryValue(query.contractId) ?? getQueryValue(query.contractcode));
  const requestedVmfCode = positiveInt(getQueryValue(query.vmfCode));
  const identifier = (getQueryValue(query.ggnumber) ?? getQueryValue(query.regnumber) ?? "").trim();
  try {
    let contract: ContractRecord | null = contractId ? await getContract(contractId) : null;
    let vehicle: ContractVehicleSearchResult | null = requestedVmfCode ? (await searchContractVehicles(String(requestedVmfCode))).find((item) => item.vmfCode === requestedVmfCode) ?? null : null;
    if (!vehicle && identifier) {
      vehicle = (await searchContractVehicles(identifier))[0] ?? null;
    }
    if (!contract && !vehicle) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Vehicle not selected</p><h2>Search for a vehicle before opening contract management.</h2><Link className="button button-secondary" href="/contracts/maintenance">Back to Contract Maintenance</Link></section></main>;

    const notice = getQueryValue(query.saved) === "1" ? "Contract captured successfully." : getQueryValue(query.updated) === "1" ? "Contract updated successfully." : getQueryValue(query.success) ? `Contract ${getQueryValue(query.success)} successfully.` : getQueryValue(query.error);
    const canApprove = hasApproverAccess(session.accessLevel, session.roles);
    const canManage = hasLoadAndManageAccess(session.roles);
    const canCancelClose = hasCancelAndCloseAccess(session.roles);
    const today = new Date().toISOString().slice(0, 10);
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="contract-detail-title"><header className="vehicle-page-header"><div><p className="eyebrow">Vehicle contract management</p><h1 id="contract-detail-title">{contract ? `Contract ${contract.contractCode}` : "Open a vehicle contract"}</h1><p>{contract ? `${valueOrDash(contract.fleetNumber)} / ${valueOrDash(contract.registrationNumber)}` : `${valueOrDash(vehicle?.fleetNumber)} / ${valueOrDash(vehicle?.registrationNumber)}`}</p></div><Link className="button button-secondary" href="/contracts/maintenance">Return to Search</Link></header>{notice ? <div className={getQueryValue(query.error) ? "notice notice-error" : "notice notice-success"} role={getQueryValue(query.error) ? "alert" : "status"}>{notice}</div> : null}{contract ? <><DetailActions contract={contract} canApprove={canApprove} canManage={canManage} canCancelClose={canCancelClose} /><ContractFacts contract={contract} />{contract.contractStatusCode === 0 || contract.contractStatusCode === 4 ? <EditForm contract={contract} today={today} /> : null}{contract.contractStatusCode === 3 && canManage ? <ExtendForm contract={contract} today={today} /> : null}{contract.contractStatusCode === 3 && canManage ? <ReassignForm contract={contract} today={today} /> : null}{contract.contractStatusCode === 3 && canCancelClose ? <CloseForm contract={contract} today={today} /> : null}</> : vehicle ? <HireForm vehicle={vehicle} /> : null}<div className="vehicle-footer-actions"><Link className="button button-secondary" href="/contracts">Contracts Menu</Link><Link className="button button-secondary" href="/home">Home</Link></div></section></main>;
  } catch (error) {
    if (error instanceof ContractApiError && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
    if (error instanceof ContractApiError && error.reason === "not-found") return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Record not found</p><h2>The selected contract was not found.</h2><Link className="button button-secondary" href="/contracts/maintenance">Back to Contracts</Link></section></main>;
    console.error("FIS contract detail request failed", error instanceof Error ? error.message : "unknown error");
    return <main className="page-shell vehicle-page-shell"><ApiUnavailable /></main>;
  }
}
