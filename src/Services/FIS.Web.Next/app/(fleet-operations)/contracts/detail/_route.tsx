import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { ClipboardList } from "lucide-react";

import ModulePageHeader from "@/components/app-shell/module-page-header";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import {
  editContractAction,
  hireContractAction,
  runContractAction,
} from "@/app/(fleet-operations)/contracts/actions";
import {
  canCaptureNewContract,
  canCloseActiveContract,
  canEditContract,
  canManageActiveContract,
  canReviewContract,
  canSubmitContract,
  hasContractAccess,
  type ContractSession,
} from "@/app/(fleet-operations)/contracts/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  ContractApiError,
  getContract,
  searchContractVehicles,
  type ContractRecord,
  type ContractVehicleSearchResult,
} from "@/lib/api/finance/api-contracts";
import { getDepartments, type DepartmentRecord } from "@/lib/api/reference-data/api-departments";
import {
  getDriverManagementSiteDrivers,
  type DriverManagementDriver,
} from "@/lib/api/reference-data/api-driver-management";
import { getSites, type SiteRecord } from "@/lib/api/reference-data/api-sites";
import { getSession } from "@/lib/auth/session";

import { loadContractDetailData, type ContractDetailData } from "./_data";

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

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function dateInputValue(value: string | null, fallback: string) {
  return value?.slice(0, 10) || fallback;
}

type ContractReferenceData = Readonly<{
  sites: SiteRecord[];
  departments: DepartmentRecord[];
  drivers: DriverManagementDriver[];
}>;

function siteOptionLabel(site: SiteRecord) {
  return `${site.description?.trim() || `Site ${site.siteCode}`} (${site.siteCode})`;
}

function departmentOptionLabel(department: DepartmentRecord) {
  return `${department.description?.trim() || `Department ${department.departmentCode}`} (${department.departmentCode})`;
}

function driverOptionLabel(driver: DriverManagementDriver, sites: SiteRecord[]) {
  const name = [driver.driverFirstname, driver.driverSurname].filter(Boolean).join(" ");
  const site = sites.find((item) => item.siteCode === driver.siteCode);
  const siteLabel = site ? siteOptionLabel(site) : `Site ${driver.siteCode}`;
  return `${name || `Site driver ${driver.siteDriverCode}`} — ${siteLabel}`;
}

function SiteSelect({
  id,
  name,
  sites,
  value,
  required = false,
}: Readonly<{
  id: string;
  name: string;
  sites: SiteRecord[];
  value?: number | null;
  required?: boolean;
}>) {
  const selectedValue = value === null || value === undefined ? "" : String(value);
  const hasSelectedValue = sites.some((site) => String(site.siteCode) === selectedValue);
  return (
    <select
      aria-label="Site"
      className="form-select"
      defaultValue={selectedValue}
      id={id}
      name={name}
      required={required}
    >
      <option value="">Select a site</option>
      {selectedValue && !hasSelectedValue ? (
        <option value={selectedValue}>{`Site ${selectedValue} (${selectedValue})`}</option>
      ) : null}
      {sites.map((site) => (
        <option key={site.siteCode} value={site.siteCode}>
          {siteOptionLabel(site)}
        </option>
      ))}
    </select>
  );
}

function DepartmentSelect({
  id,
  name,
  departments,
  value,
  required = false,
}: Readonly<{
  id: string;
  name: string;
  departments: DepartmentRecord[];
  value?: number | null;
  required?: boolean;
}>) {
  const selectedValue = value === null || value === undefined ? "" : String(value);
  const hasSelectedValue = departments.some(
    (department) => String(department.departmentCode) === selectedValue,
  );
  return (
    <select
      aria-label="Department"
      className="form-select"
      defaultValue={selectedValue}
      id={id}
      name={name}
      required={required}
    >
      <option value="">Select a department</option>
      {selectedValue && !hasSelectedValue ? (
        <option value={selectedValue}>{`Department ${selectedValue} (${selectedValue})`}</option>
      ) : null}
      {departments.map((department) => (
        <option key={department.departmentCode} value={department.departmentCode}>
          {departmentOptionLabel(department)}
        </option>
      ))}
    </select>
  );
}

function SiteDriverSelect({
  id,
  name,
  drivers,
  sites,
  value,
  required = false,
}: Readonly<{
  id: string;
  name: string;
  drivers: DriverManagementDriver[];
  sites: SiteRecord[];
  value?: number | null;
  required?: boolean;
}>) {
  const selectedValue = value === null || value === undefined ? "" : String(value);
  const hasSelectedValue = drivers.some(
    (driver) => String(driver.siteDriverCode) === selectedValue,
  );
  return (
    <select
      aria-label="Site driver"
      className="form-select"
      defaultValue={selectedValue}
      id={id}
      name={name}
      required={required}
    >
      <option value="">Select a site driver</option>
      {selectedValue && !hasSelectedValue ? (
        <option value={selectedValue}>{`Site driver ${selectedValue} (${selectedValue})`}</option>
      ) : null}
      {drivers.map((driver) => (
        <option key={driver.siteDriverCode} value={driver.siteDriverCode}>
          {driverOptionLabel(driver, sites)}
        </option>
      ))}
    </select>
  );
}

function statusLabel(contract: ContractRecord) {
  switch (contract.contractStatusCode) {
    case 0:
      return "Draft";
    case 1:
      return "Pending Review";
    case 2:
      return "Approved";
    case 3:
      return "Active";
    case 4:
      return "Declined for Correction";
    case 5:
      return "Declined";
    case 6:
      return "Cancelled";
    case 7:
      return "Closed";
    default:
      return contract.stillCurrent?.toUpperCase() === "Y" ? "Active" : "Unknown";
  }
}

type DetailActionState = Readonly<{
  canActivate: boolean;
  canCancelClose: boolean;
  canEdit: boolean;
  canManage: boolean;
  canRecall: boolean;
  canReview: boolean;
  canSubmit: boolean;
  isActive: boolean;
  status: number | null;
}>;

function getDetailActionState(
  contract: ContractRecord,
  session: ContractSession,
): DetailActionState {
  const status = contract.contractStatusCode;
  const isActive =
    status === 3 || (status === null && contract.stillCurrent?.toUpperCase() === "Y");
  return {
    canActivate: (status === 1 || status === 2) && canReviewContract(contract, session),
    canCancelClose: isActive && canCloseActiveContract(session.roles),
    canEdit: (status === 0 || status === 4) && canEditContract(contract, session),
    canManage: isActive && canManageActiveContract(session.roles),
    canRecall: status === 1 && canSubmitContract(contract, session),
    canReview: status === 1 && canReviewContract(contract, session),
    canSubmit: (status === 0 || status === 4) && canSubmitContract(contract, session),
    isActive,
    status,
  };
}

function ActionLink({
  children,
  href,
  show,
}: Readonly<{ children: React.ReactNode; href: string; show: boolean }>) {
  if (!show) return null;
  return (
    <Link className="button button-secondary" href={href}>
      {children}
    </Link>
  );
}

function ActionForm({
  action,
  buttonClassName = "button button-secondary",
  children,
  contract,
  returnPath,
  show,
}: Readonly<{
  action: string;
  buttonClassName?: string;
  children: React.ReactNode;
  contract: ContractRecord;
  returnPath: string;
  show: boolean;
}>) {
  if (!show) return null;
  return (
    <form action={runContractAction}>
      <input name="contractId" type="hidden" value={contract.contractCode} />
      <input name="returnPath" type="hidden" value={returnPath} />
      <input name="action" type="hidden" value={action} />
      <button className={buttonClassName} type="submit">
        {children}
      </button>
    </form>
  );
}

function WorkflowActionForms({
  contract,
  returnPath,
  state,
}: Readonly<{ contract: ContractRecord; returnPath: string; state: DetailActionState }>) {
  return (
    <>
      <ActionForm
        action="submit"
        contract={contract}
        returnPath={returnPath}
        show={state.canSubmit}
      >
        Submit for approval
      </ActionForm>
      <ActionForm
        action="recall"
        contract={contract}
        returnPath={returnPath}
        show={state.canRecall}
      >
        Recall to draft
      </ActionForm>
      <ActionForm
        action="approve-activate"
        buttonClassName="button button-primary"
        contract={contract}
        returnPath={returnPath}
        show={state.canReview}
      >
        Approve and activate
      </ActionForm>
      <ActionForm
        action="approve-activate"
        buttonClassName="button button-primary"
        contract={contract}
        returnPath={returnPath}
        show={state.status === 2 && state.canActivate}
      >
        Activate approved contract
      </ActionForm>
    </>
  );
}

function LifecycleActionLinks({
  contract,
  returnPath,
  state,
}: Readonly<{ contract: ContractRecord; returnPath: string; state: DetailActionState }>) {
  return (
    <>
      <ActionLink href={`${returnPath}#extend-contract`} show={state.canManage}>
        Extend
      </ActionLink>
      <ActionLink href={`${returnPath}#reassign-contract`} show={state.canManage}>
        Reassign contract
      </ActionLink>
      <ActionLink href={`${returnPath}#close-contract`} show={state.canCancelClose}>
        Close and return home
      </ActionLink>
      <ActionForm
        action="cancel"
        buttonClassName="button button-danger"
        contract={contract}
        returnPath={returnPath}
        show={state.canCancelClose}
      >
        Cancel
      </ActionForm>
      <ActionLink
        href={`/contracts/relief-vehicle-search?contractId=${contract.contractCode}`}
        show={state.canManage && contract.reliefVehicleOption === true}
      >
        Create relief contract
      </ActionLink>
    </>
  );
}

function ReviewActionForm({
  contract,
  returnPath,
  state,
}: Readonly<{ contract: ContractRecord; returnPath: string; state: DetailActionState }>) {
  if (!state.canReview) return null;
  return (
    <form action={runContractAction} className="form-grid contract-action-form">
      <input name="contractId" type="hidden" value={contract.contractCode} />
      <input name="returnPath" type="hidden" value={returnPath} />
      <label className="form-field form-group-full">
        <span className="form-label">Approval notes</span>
        <textarea className="form-textarea" name="approvalNotes" rows={2} />
      </label>
      <div className="button-row">
        <button className="button button-secondary" name="action" value="approve" type="submit">
          Approve only
        </button>
        <button
          className="button button-warning"
          name="action"
          value="decline-correction"
          type="submit"
        >
          Return for correction
        </button>
        <button className="button button-danger" name="action" value="decline" type="submit">
          Decline
        </button>
      </div>
    </form>
  );
}

function DetailActions({
  contract,
  session,
}: Readonly<{
  contract: ContractRecord;
  session: ContractSession;
}>) {
  const returnPath = `/contracts/detail?contractId=${contract.contractCode}`;
  const state = getDetailActionState(contract, session);
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="contract-actions-title">
      <p className="eyebrow">Available actions</p>
      <h2 id="contract-actions-title">{statusLabel(contract)}</h2>
      <div className="button-row">
        <ActionLink href={`${returnPath}#edit-contract`} show={state.canEdit}>
          Edit contract
        </ActionLink>
        <WorkflowActionForms contract={contract} returnPath={returnPath} state={state} />
        <LifecycleActionLinks contract={contract} returnPath={returnPath} state={state} />
      </div>
      <ReviewActionForm contract={contract} returnPath={returnPath} state={state} />
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
    [
      "Collector",
      [contract.collectorFirstname, contract.collectorSurname].filter(Boolean).join(" ") || "-",
    ],
    ["Collector SA ID", valueOrDash(contract.collectorSaId)],
    ["Collector passport", valueOrDash(contract.collectorPassportNumber)],
    ["Collector office number", valueOrDash(contract.collectorOfficeNumber)],
    ["Collector cellphone", valueOrDash(contract.collectorCellphoneNumber)],
    ["Collector office", valueOrDash(contract.collectorOffice)],
    ["Collector designation", valueOrDash(contract.collectorDesignation)],
    [
      "Relief vehicle option",
      contract.reliefVehicleOption === null ? "-" : contract.reliefVehicleOption ? "Yes" : "No",
    ],
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
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="contract-facts-title">
      <p className="eyebrow">Legacy contract record</p>
      <h2 id="contract-facts-title">Contract details</h2>
      <dl className="contract-facts">
        {facts.map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>{value}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}

function HireForm({
  vehicle,
  references,
  today,
}: Readonly<{
  vehicle: ContractVehicleSearchResult;
  references: ContractReferenceData;
  today: string;
}>) {
  return (
    <form action={hireContractAction} className="vehicle-status-maintenance-panel">
      <input name="returnPath" type="hidden" value="/contracts/maintenance" />
      <input name="vmfCode" type="hidden" value={vehicle.vmfCode} />
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">New contract</p>
          <h2>Open contract for {valueOrDash(vehicle.fleetNumber)}</h2>
          <p>
            {valueOrDash(vehicle.registrationNumber)} ({vehicle.vmfCode})
          </p>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="new-contract-site">
            Site
          </label>
          <SiteSelect id="new-contract-site" name="siteCode" required sites={references.sites} />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="new-contract-start">
            Contract start date
          </label>
          <input
            className="form-input"
            id="new-contract-start"
            name="startDate"
            type="date"
            defaultValue={today}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="new-contract-odo">
            Start odometer
          </label>
          <input
            className="form-input"
            id="new-contract-odo"
            min="0"
            name="startOdometer"
            type="number"
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="new-contract-driver">
            Driver ID
          </label>
          <input className="form-input" id="new-contract-driver" maxLength={60} name="driverId" />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="new-contract-site-driver">
            Site driver
          </label>
          <SiteDriverSelect
            drivers={references.drivers}
            id="new-contract-site-driver"
            name="siteDriverCode"
            sites={references.sites}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="new-contract-return">
            Target return date
          </label>
          <input
            className="form-input"
            id="new-contract-return"
            name="targetReturnDate"
            type="date"
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="new-contract-auth">
            Authorisation
          </label>
          <input
            className="form-input"
            id="new-contract-auth"
            maxLength={60}
            name="authorisation"
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="new-contract-notes">
            Notes
          </label>
          <textarea
            className="form-textarea"
            id="new-contract-notes"
            maxLength={1000}
            name="notes"
            rows={4}
          />
        </div>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Open contract
        </button>
        <Link className="button button-secondary" href="/contracts/maintenance">
          Cancel
        </Link>
      </div>
    </form>
  );
}

function EditForm({
  contract,
  references,
  today,
}: Readonly<{
  contract: ContractRecord;
  references: ContractReferenceData;
  today: string;
}>) {
  return (
    <form
      action={editContractAction}
      className="vehicle-status-maintenance-panel"
      id="edit-contract"
    >
      <input name="contractId" type="hidden" value={contract.contractCode} />
      <input
        name="returnPath"
        type="hidden"
        value={`/contracts/detail?contractId=${contract.contractCode}`}
      />
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Editable legacy fields</p>
          <h2>Edit contract {contract.contractCode}</h2>
          <p>Only draft and correction-request contracts can be edited.</p>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="edit-contract-site">
            Site
          </label>
          <SiteSelect
            id="edit-contract-site"
            name="siteCode"
            required
            sites={references.sites}
            value={contract.siteCode}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="edit-contract-odo">
            Start odometer
          </label>
          <input
            className="form-input"
            id="edit-contract-odo"
            min="0"
            name="startOdometer"
            type="number"
            defaultValue={contract.startOdometer ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="edit-contract-driver">
            Driver ID
          </label>
          <input
            className="form-input"
            id="edit-contract-driver"
            maxLength={60}
            name="driverId"
            defaultValue={contract.driverId ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="edit-contract-site-driver">
            Site driver
          </label>
          <SiteDriverSelect
            drivers={references.drivers}
            id="edit-contract-site-driver"
            name="siteDriverCode"
            sites={references.sites}
            value={contract.siteDriverCode}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="edit-contract-return">
            Target return date
          </label>
          <input
            className="form-input"
            id="edit-contract-return"
            name="targetReturnDate"
            type="date"
            defaultValue={dateInputValue(contract.targetReturnDate, today)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="edit-contract-auth">
            Authorisation
          </label>
          <input
            className="form-input"
            id="edit-contract-auth"
            maxLength={60}
            name="authorisation"
            defaultValue={contract.authorisation ?? ""}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="edit-contract-notes">
            Notes
          </label>
          <textarea
            className="form-textarea"
            id="edit-contract-notes"
            maxLength={1000}
            name="notes"
            rows={4}
            defaultValue={contract.notes ?? ""}
          />
        </div>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Save contract
        </button>
        <Link
          className="button button-secondary"
          href={`/contracts/detail?contractId=${contract.contractCode}`}
        >
          Cancel
        </Link>
      </div>
    </form>
  );
}

function ExtendForm({ contract, today }: Readonly<{ contract: ContractRecord; today: string }>) {
  return (
    <form
      action={runContractAction}
      className="vehicle-status-maintenance-panel"
      id="extend-contract"
    >
      <input name="contractId" type="hidden" value={contract.contractCode} />
      <input
        name="returnPath"
        type="hidden"
        value={`/contracts/detail?contractId=${contract.contractCode}`}
      />
      <input name="action" type="hidden" value="extend" />
      <div className="form-field">
        <label className="form-label" htmlFor="new-target-return">
          New target return date
        </label>
        <input
          className="form-input"
          id="new-target-return"
          name="newTargetReturnDate"
          type="date"
          defaultValue={dateInputValue(contract.targetReturnDate, today)}
          required
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="extend-estimated-kilometres">
          Estimated overall kilometres
        </label>
        <input
          className="form-input"
          id="extend-estimated-kilometres"
          min="0"
          name="estimatedOverallKilometres"
          type="number"
          defaultValue={contract.contractEstimatedOverallKm ?? ""}
        />
      </div>
      <div className="form-field form-group-full">
        <label className="form-label" htmlFor="extend-notes">
          Extension notes
        </label>
        <textarea
          className="form-input"
          id="extend-notes"
          name="extensionNotes"
          rows={3}
          defaultValue={contract.notes ?? ""}
        />
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Save extension
        </button>
        <Link
          className="button button-secondary"
          href={`/contracts/detail?contractId=${contract.contractCode}`}
        >
          Cancel
        </Link>
      </div>
    </form>
  );
}

function ReassignForm({
  contract,
  references,
  today,
}: Readonly<{
  contract: ContractRecord;
  references: ContractReferenceData;
  today: string;
}>) {
  return (
    <form
      action={runContractAction}
      className="vehicle-status-maintenance-panel"
      id="reassign-contract"
    >
      <input name="contractId" type="hidden" value={contract.contractCode} />
      <input
        name="returnPath"
        type="hidden"
        value={`/contracts/detail?contractId=${contract.contractCode}`}
      />
      <input name="action" type="hidden" value="reassign" />
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Reassign active contract</p>
          <h2>Update site or custodian</h2>
          <p>
            The legacy workflow creates a new effective contract record and closes the previous
            active record. Select a new site, custodian, or both.
          </p>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="reassign-site">
            Destination site
          </label>
          <SiteSelect id="reassign-site" name="newSiteCode" sites={references.sites} />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="reassign-driver">
            Destination custodian driver
          </label>
          <SiteDriverSelect
            drivers={references.drivers}
            id="reassign-driver"
            name="newSiteDriverCode"
            sites={references.sites}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="reassign-start-date">
            Effective start date
          </label>
          <input
            className="form-input"
            id="reassign-start-date"
            name="reassignStartDate"
            type="date"
            defaultValue={today}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="reassign-start-odo">
            Start odometer
          </label>
          <input
            className="form-input"
            id="reassign-start-odo"
            min="0"
            name="reassignStartOdometer"
            type="number"
            defaultValue={contract.startOdometer ?? ""}
            required
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="reassign-reason">
            Reason
          </label>
          <textarea
            className="form-textarea"
            id="reassign-reason"
            maxLength={1000}
            name="reassignReason"
            rows={3}
            defaultValue="Reassigned from contract detail."
          />
        </div>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Save reassignment
        </button>
        <Link
          className="button button-secondary"
          href={`/contracts/detail?contractId=${contract.contractCode}`}
        >
          Cancel
        </Link>
      </div>
    </form>
  );
}

function CloseForm({
  contract,
  references,
  today,
}: Readonly<{
  contract: ContractRecord;
  references: ContractReferenceData;
  today: string;
}>) {
  return (
    <form
      action={runContractAction}
      className="vehicle-status-maintenance-panel"
      id="close-contract"
    >
      <input name="contractId" type="hidden" value={contract.contractCode} />
      <input
        name="returnPath"
        type="hidden"
        value={`/contracts/detail?contractId=${contract.contractCode}`}
      />
      <input name="action" type="hidden" value="close" />
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Close active contract</p>
          <h2>Return vehicle home</h2>
          <p>The home department and site are required by the established closing workflow.</p>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="close-end-date">
            Close date
          </label>
          <input
            className="form-input"
            id="close-end-date"
            name="endDate"
            type="date"
            defaultValue={today}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="close-end-odo">
            Close odometer
          </label>
          <input
            className="form-input"
            id="close-end-odo"
            min="0"
            name="endOdometer"
            type="number"
            defaultValue={contract.endOdometer ?? ""}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="home-department">
            Home department
          </label>
          <DepartmentSelect
            id="home-department"
            name="homeDepartmentCode"
            required
            departments={references.departments}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="home-site">
            Home site
          </label>
          <SiteSelect id="home-site" name="homeSiteCode" required sites={references.sites} />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="home-driver">
            Home site driver
          </label>
          <SiteDriverSelect
            drivers={references.drivers}
            id="home-driver"
            name="homeSiteDriverCode"
            sites={references.sites}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="close-notes">
            Close notes
          </label>
          <textarea
            className="form-textarea"
            id="close-notes"
            maxLength={1000}
            name="closeNotes"
            rows={3}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="vehicle-checkbox-label">
            <input name="createHomeCustodyContract" type="checkbox" value="true" defaultChecked />
            Create home custody contract
          </label>
        </div>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Close and return home
        </button>
        <Link
          className="button button-secondary"
          href={`/contracts/detail?contractId=${contract.contractCode}`}
        >
          Cancel
        </Link>
      </div>
    </form>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Contract details could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href="/contracts/maintenance">
        Back to Contracts
      </Link>
    </section>
  );
}

function ContractExistingView({
  data,
  session,
}: Readonly<{
  data: Extract<ContractDetailData, { kind: "ok" }>;
  session: ContractSession;
}>) {
  if (!data.contract) return null;
  return (
    <>
      <DetailActions contract={data.contract} session={session} />
      <ContractFacts contract={data.contract} />
      {data.canEdit ? (
        <EditForm contract={data.contract} references={data.references} today={data.today} />
      ) : null}
      {data.isActive && data.canManage ? (
        <ExtendForm contract={data.contract} today={data.today} />
      ) : null}
      {data.isActive && data.canManage ? (
        <ReassignForm contract={data.contract} references={data.references} today={data.today} />
      ) : null}
      {data.isActive && data.canCancelClose ? (
        <CloseForm contract={data.contract} references={data.references} today={data.today} />
      ) : null}
    </>
  );
}

function ContractVehicleView({
  data,
}: Readonly<{ data: Extract<ContractDetailData, { kind: "ok" }> }>) {
  if (!data.vehicle) return null;
  return data.canCapture ? (
    <HireForm references={data.references} today={data.today} vehicle={data.vehicle} />
  ) : (
    <p className="muted-copy">You can view this vehicle but do not have capture access.</p>
  );
}

function ContractDetailView({
  data,
  notice,
  session,
}: Readonly<{
  data: Extract<ContractDetailData, { kind: "ok" }>;
  notice?: { isError: boolean; message: string };
  session: ContractSession;
}>) {
  const title = data.contract
    ? `Contract ${data.contract.contractCode}`
    : "Open a vehicle contract";
  const description = data.contract
    ? `${valueOrDash(data.contract.fleetNumber)} / ${valueOrDash(data.contract.registrationNumber)}`
    : `${valueOrDash(data.vehicle?.fleetNumber)} / ${valueOrDash(data.vehicle?.registrationNumber)}`;
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="contract-detail-title">
        <ModulePageHeader
          icon={ClipboardList}
          eyebrow="Vehicle contract management"
          title={title}
          titleId="contract-detail-title"
          description={description}
          actions={
            <Link className="button button-secondary" href="/contracts/maintenance">
              Return to Search
            </Link>
          }
        />
        {notice ? (
          <div
            className={notice.isError ? "notice notice-error" : "notice notice-success"}
            role={notice.isError ? "alert" : "status"}
          >
            {notice.message}
          </div>
        ) : null}
        {data.contract ? (
          <ContractExistingView data={data} session={session} />
        ) : (
          <ContractVehicleView data={data} />
        )}
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/contracts">
            Contracts Menu
          </Link>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </div>
      </section>
    </main>
  );
}

const ContractDetailPageContent = renderContractDetailPageContent;

async function renderContractDetailPageContent({
  searchParams,
  routePath = "/contracts/detail",
}: ContractDetailPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  if (!hasContractAccess(session.accessLevel, session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to maintain vehicle contracts.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const contractId = positiveInt(
    getQueryValue(query.contractId) ?? getQueryValue(query.contractcode),
  );
  const requestedVmfCode = positiveInt(getQueryValue(query.vmfCode));
  const identifier = (getQueryValue(query.ggnumber) ?? getQueryValue(query.regnumber) ?? "").trim();
  try {
    let contract: ContractRecord | null = contractId ? await getContract(contractId) : null;
    let vehicle: ContractVehicleSearchResult | null = requestedVmfCode
      ? ((await searchContractVehicles(String(requestedVmfCode))).find(
          (item) => item.vmfCode === requestedVmfCode,
        ) ?? null)
      : null;
    if (!vehicle && identifier) {
      vehicle = (await searchContractVehicles(identifier))[0] ?? null;
    }
    if (!contract && !vehicle)
      return (
        <main className="page-shell vehicle-page-shell">
          <section className="vehicle-status-card" role="alert">
            <p className="eyebrow">Vehicle not selected</p>
            <h2>Search for a vehicle before opening contract management.</h2>
            <Link className="button button-secondary" href="/contracts/maintenance">
              Back to Contract Maintenance
            </Link>
          </section>
        </main>
      );

    const canCapture = !contract && vehicle ? canCaptureNewContract(session.roles) : false;
    const canEdit =
      contract && [0, 4].includes(contract.contractStatusCode ?? -1)
        ? canEditContract(contract, session)
        : false;
    const isActive =
      contract?.contractStatusCode === 3 ||
      (contract?.contractStatusCode === null && contract.stillCurrent?.toUpperCase() === "Y");
    const canManage = isActive ? canManageActiveContract(session.roles) : false;
    const canCancelClose = isActive ? canCloseActiveContract(session.roles) : false;
    const needsReferences = Boolean(canCapture || canEdit || canManage || canCancelClose);
    let references: ContractReferenceData = { sites: [], departments: [], drivers: [] };
    if (needsReferences) {
      const [sites, departments, drivers] = await Promise.all([
        getSites(),
        getDepartments(),
        getDriverManagementSiteDrivers(),
      ]);
      references = { sites, departments, drivers };
    }

    const notice =
      getQueryValue(query.saved) === "1"
        ? "Contract captured successfully."
        : getQueryValue(query.updated) === "1"
          ? "Contract updated successfully."
          : getQueryValue(query.success)
            ? `Contract ${getQueryValue(query.success)} successfully.`
            : getQueryValue(query.error);
    const today = new Date().toISOString().slice(0, 10);
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="contract-detail-title">
          <ModulePageHeader
            icon={ClipboardList}
            eyebrow="Vehicle contract management"
            title={contract ? `Contract ${contract.contractCode}` : "Open a vehicle contract"}
            titleId="contract-detail-title"
            description={
              contract
                ? `${valueOrDash(contract.fleetNumber)} / ${valueOrDash(contract.registrationNumber)}`
                : `${valueOrDash(vehicle?.fleetNumber)} / ${valueOrDash(vehicle?.registrationNumber)}`
            }
            actions={
              <Link className="button button-secondary" href="/contracts/maintenance">
                Return to Search
              </Link>
            }
          />
          {notice ? (
            <div
              className={
                getQueryValue(query.error) ? "notice notice-error" : "notice notice-success"
              }
              role={getQueryValue(query.error) ? "alert" : "status"}
            >
              {notice}
            </div>
          ) : null}
          {contract ? (
            <>
              <DetailActions contract={contract} session={session} />
              <ContractFacts contract={contract} />
              {canEdit ? (
                <EditForm contract={contract} references={references} today={today} />
              ) : null}
              {isActive && canManage ? <ExtendForm contract={contract} today={today} /> : null}
              {isActive && canManage ? (
                <ReassignForm contract={contract} references={references} today={today} />
              ) : null}
              {isActive && canCancelClose ? (
                <CloseForm contract={contract} references={references} today={today} />
              ) : null}
            </>
          ) : vehicle ? (
            canCapture ? (
              <HireForm references={references} today={today} vehicle={vehicle} />
            ) : (
              <p className="muted-copy">
                You can view this vehicle but do not have capture access.
              </p>
            )
          ) : null}
          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/contracts">
              Contracts Menu
            </Link>
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          </div>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof ContractApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    if (error instanceof ContractApiError && error.reason === "not-found")
      return (
        <main className="page-shell vehicle-page-shell">
          <section className="vehicle-status-card" role="alert">
            <p className="eyebrow">Record not found</p>
            <h2>The selected contract was not found.</h2>
            <Link className="button button-secondary" href="/contracts/maintenance">
              Back to Contracts
            </Link>
          </section>
        </main>
      );
    console.error(
      "FIS contract detail request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
}

export function ContractDetailRoute(props: ContractDetailPageProps) {
  return (
    <StreamedRoute>
      <ContractDetailPageContent {...props} />
    </StreamedRoute>
  );
}

export default function ContractDetailPage({
  searchParams,
}: Pick<ContractDetailPageProps, "searchParams">) {
  return <ContractDetailRoute searchParams={searchParams} />;
}
