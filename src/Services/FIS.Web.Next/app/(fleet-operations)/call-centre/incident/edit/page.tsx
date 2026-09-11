import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { updateCallCentreIncidentAction } from "@/app/(fleet-operations)/call-centre/incident/edit/actions";
import {
  IncidentClosureFields,
  IncidentRecordFields,
} from "@/app/(fleet-operations)/call-centre/incident/edit/incident-editor-sections";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import {
  CallCentreApiError,
  getCallCentreEditDetails,
  getCallCentreIncident,
  getCallCentreSites,
  getCallCentreLossTypes,
  getCallCentreTowTrucks,
  type CallCentreIncidentRecord,
  type CallCentreEditDetails,
  type CallCentreSiteOption,
  type LossTypeOption,
  type TowTruckOption,
} from "@/lib/api/fleet-operations/api-call-centre";
import { getNotifyLists, type NotifyListRecord } from "@/lib/api/administration/api-notify-list";
import { getSession } from "@/lib/auth/session";

const CALL_CENTRE_ROLE = "Call Centre";
const EMPTY_CALL_CENTRE_SITES: CallCentreSiteOption[] = [];
const EMPTY_NOTIFY_LISTS: NotifyListRecord[] = [];
const EMPTY_EDIT_DETAILS: CallCentreEditDetails = {
  accidentTableAvailable: false,
  accident: null,
  lossTableAvailable: false,
  loss: null,
  towingTableAvailable: false,
  towing: null,
};
type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export type IncidentEditPageProps = { searchParams: SearchParams };

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrEmpty(value: string | number | null | undefined) {
  return value === null || value === undefined ? "" : String(value);
}

function dateInputValue(value: string | null) {
  return value?.match(/^\d{4}-\d{2}-\d{2}/)?.[0] ?? "";
}

function timeInputValue(value: string | null) {
  if (!value) return "";
  const isoMatch = value.match(/T(\d{2}:\d{2})/);
  if (isoMatch) return isoMatch[1];
  return value.match(/^(\d{2}:\d{2})/)?.[1] ?? "";
}

function childValue(record: Record<string, unknown> | null, ...keys: string[]) {
  if (!record) return "";
  for (const key of keys) {
    const value = record[key];
    if (typeof value === "string" || typeof value === "number") return String(value);
  }
  return "";
}

function childNumber(record: Record<string, unknown> | null, ...keys: string[]) {
  const value = childValue(record, ...keys);
  const parsed = Number(value);
  return value && Number.isInteger(parsed) ? parsed : null;
}

function childDate(record: Record<string, unknown> | null, ...keys: string[]) {
  return dateInputValue(childValue(record, ...keys) || null);
}

function childTime(record: Record<string, unknown> | null, ...keys: string[]) {
  return timeInputValue(childValue(record, ...keys) || null);
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to update call centre incidents.</h2>
      <p className="muted-copy">This page requires the Call Centre role.</p>
    </section>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Service unavailable</p>
      <h2>The call centre incident could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href="/call-centre/incident/edit">
        Try again
      </Link>
    </section>
  );
}

function LookupForm({ referenceNumber }: Readonly<{ referenceNumber: string }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <div className="field">
        <label htmlFor="incident-reference">Reference Number (GMT)</label>
        <input
          id="incident-reference"
          name="referenceNumber"
          inputMode="numeric"
          defaultValue={referenceNumber}
          required
        />
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href="/call-centre">
          Menu
        </Link>
      </div>
    </form>
  );
}

function Field({
  id,
  label,
  name,
  defaultValue,
  maxLength,
  type = "text",
}: Readonly<{
  id: string;
  label: string;
  name: string;
  defaultValue: string;
  maxLength?: number;
  type?: string;
}>) {
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <input id={id} name={name} type={type} defaultValue={defaultValue} maxLength={maxLength} />
    </div>
  );
}

function SiteSelect({
  id,
  label,
  name,
  value,
  sites,
}: Readonly<{
  id: string;
  label: string;
  name: string;
  value: number | null;
  sites: CallCentreSiteOption[];
}>) {
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <select id={id} name={name} defaultValue={valueOrEmpty(value)}>
        <option value="">Select site</option>
        {sites.map((site) => (
          <option key={site.code} value={site.code}>
            {site.departmentNumber ? `${site.departmentNumber} - ` : ""}
            {site.description}
          </option>
        ))}
      </select>
    </div>
  );
}

function ChoiceSelect({
  id,
  label,
  name,
  value,
  choices,
}: Readonly<{
  id: string;
  label: string;
  name: string;
  value: string | null;
  choices: readonly string[];
}>) {
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <select id={id} name={name} defaultValue={valueOrEmpty(value)}>
        <option value="">Select</option>
        {choices.map((choice) => (
          <option key={choice} value={choice}>
            {choice === "Y" ? "Yes" : choice === "N" ? "No" : choice}
          </option>
        ))}
      </select>
    </div>
  );
}

function AccidentChildFields({
  record,
  sites,
  tableAvailable,
}: Readonly<{
  record: Record<string, unknown> | null;
  sites: CallCentreSiteOption[];
  tableAvailable: boolean;
}>) {
  if (!record)
    return (
      <section className="vehicle-form-section" aria-labelledby="accident-child-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy Accident child table</p>
            <h2 id="accident-child-title">Accident details</h2>
          </div>
        </div>
        <p className="muted-copy">
          {tableAvailable
            ? "No linked Accident row was found. The Call_centre record remains editable."
            : "This database does not expose the legacy Accident columns. The Call_centre record remains editable."}
        </p>
      </section>
    );
  return (
    <section className="vehicle-form-section" aria-labelledby="accident-child-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Legacy Accident child table</p>
          <h2 id="accident-child-title">Accident details</h2>
        </div>
      </div>
      <div className="field-grid">
        <Field
          id="accident-occurrence-date"
          label="Accident Date"
          name="accident_occurence_date"
          type="date"
          defaultValue={childDate(record, "occurence_date")}
        />
        <Field
          id="accident-occurrence-time"
          label="Accident Time"
          name="accident_occurence_time"
          type="time"
          defaultValue={childTime(record, "occurence_time")}
        />
        <Field
          id="accident-description"
          label="Accident Description"
          name="accident_description"
          maxLength={60}
          defaultValue={childValue(record, "description")}
        />
        <Field
          id="accident-damage"
          label="GG Damage Description"
          name="accident_damage_description"
          maxLength={60}
          defaultValue={childValue(record, "damage_description")}
        />
        <Field
          id="accident-third-party-reg"
          label="Third Party Registration"
          name="accident_third_party_regno"
          maxLength={8}
          defaultValue={childValue(record, "third_party_regno")}
        />
        <Field
          id="accident-third-party-owner"
          label="Third Party Owner"
          name="accident_third_party_owner"
          maxLength={30}
          defaultValue={childValue(record, "third_party_owner")}
        />
        <Field
          id="accident-third-party-tel"
          label="Third Party Telephone"
          name="accident_third_party_tel"
          maxLength={30}
          defaultValue={childValue(record, "third_party_tel")}
        />
        <Field
          id="accident-notes"
          label="Accident Notes"
          name="accident_notes"
          maxLength={50}
          defaultValue={childValue(record, "notes")}
        />
        <Field
          id="accident-occurrence-place"
          label="Accident Place"
          name="accident_occurence_place"
          maxLength={50}
          defaultValue={childValue(record, "occurence_place")}
        />
        <SiteSelect
          id="accident-driver-site"
          label="Accident Driver Site"
          name="accident_driver_site_code"
          value={childNumber(record, "driver_site_code")}
          sites={sites}
        />
        <ChoiceSelect
          id="accident-death"
          label="Death"
          name="accident_death"
          value={childValue(record, "death")}
          choices={["?", "Y", "N"]}
        />
        <ChoiceSelect
          id="accident-injured"
          label="Injured"
          name="accident_injured"
          value={childValue(record, "injured")}
          choices={["?", "Y", "N"]}
        />
        <ChoiceSelect
          id="accident-tow-need"
          label="Need Tow Truck"
          name="accident_tow_need"
          value={childValue(record, "Tow_need", "tow_need")}
          choices={["Y", "N"]}
        />
      </div>
    </section>
  );
}

function LossChildFields({
  record,
  sites,
  lossTypes,
  tableAvailable,
}: Readonly<{
  record: Record<string, unknown> | null;
  sites: CallCentreSiteOption[];
  lossTypes: LossTypeOption[];
  tableAvailable: boolean;
}>) {
  if (!record)
    return (
      <section className="vehicle-form-section" aria-labelledby="loss-child-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy Losses child table</p>
            <h2 id="loss-child-title">Loss / theft details</h2>
          </div>
        </div>
        <p className="muted-copy">
          {tableAvailable
            ? "No linked Losses row was found. The Call_centre record remains editable."
            : "This database does not expose the legacy Losses columns. The Call_centre record remains editable."}
        </p>
      </section>
    );
  return (
    <section className="vehicle-form-section" aria-labelledby="loss-child-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Legacy Losses child table</p>
          <h2 id="loss-child-title">Loss / theft details</h2>
        </div>
      </div>
      <div className="field-grid">
        <Field
          id="loss-date"
          label="Loss Date"
          name="loss_date"
          type="date"
          defaultValue={childDate(record, "loss_date")}
        />
        <div className="field">
          <label htmlFor="loss-type">Loss Type</label>
          <select
            id="loss-type"
            name="loss_type_code"
            defaultValue={valueOrEmpty(childNumber(record, "loss_type_code"))}
          >
            <option value="">Select loss type</option>
            {lossTypes.map((item) => (
              <option key={item.code} value={item.code}>
                {item.description}
              </option>
            ))}
          </select>
        </div>
        <SiteSelect
          id="loss-site"
          label="Loss Site"
          name="loss_site_code"
          value={childNumber(record, "site_code")}
          sites={sites}
        />
        <Field
          id="loss-department-contact"
          label="Department Contact"
          name="loss_department_contact"
          maxLength={100}
          defaultValue={childValue(record, "dept_contact")}
        />
        <Field
          id="loss-place"
          label="Place of Loss"
          name="loss_place_of_loss"
          maxLength={100}
          defaultValue={childValue(record, "place_of_loss")}
        />
        <Field
          id="loss-driver"
          label="Loss Driver"
          name="loss_driver_name"
          maxLength={100}
          defaultValue={childValue(record, "driver_name")}
        />
        <Field
          id="loss-remarks"
          label="Loss Remarks"
          name="loss_remarks"
          maxLength={100}
          defaultValue={childValue(record, "Remarks", "remarks")}
        />
        <ChoiceSelect
          id="loss-tow-need"
          label="Need Tow Truck"
          name="loss_tow_need"
          value={childValue(record, "Tow_need", "tow_need")}
          choices={["Y", "N"]}
        />
      </div>
    </section>
  );
}

function TowingChildFields({
  record,
  sites,
  towTrucks,
  tableAvailable,
}: Readonly<{
  record: Record<string, unknown> | null;
  sites: CallCentreSiteOption[];
  towTrucks: TowTruckOption[];
  tableAvailable: boolean;
}>) {
  if (!record)
    return (
      <section className="vehicle-form-section" aria-labelledby="towing-child-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy Towing child table</p>
            <h2 id="towing-child-title">Road assistance details</h2>
          </div>
        </div>
        <p className="muted-copy">
          {tableAvailable
            ? "No linked Towing row was found. The Call_centre record remains editable."
            : "This database does not expose the legacy Towing columns. The Call_centre record remains editable."}
        </p>
      </section>
    );
  return (
    <section className="vehicle-form-section" aria-labelledby="towing-child-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Legacy Towing child table</p>
          <h2 id="towing-child-title">Road assistance details</h2>
        </div>
      </div>
      <div className="field-grid">
        <Field
          id="towing-location"
          label="Towing Location"
          name="towing_location"
          maxLength={50}
          defaultValue={childValue(record, "Tow_location_start")}
        />
        <Field
          id="towing-problem"
          label="Vehicle Problem"
          name="towing_vehicle_problem"
          maxLength={60}
          defaultValue={childValue(record, "Vehicle_problem")}
        />
        <SiteSelect
          id="towing-site"
          label="Towing Site"
          name="towing_site_code"
          value={childNumber(record, "Site_code")}
          sites={sites}
        />
        <div className="field">
          <label htmlFor="towing-truck">Assist Company</label>
          <select
            id="towing-truck"
            name="towing_tow_truck_code"
            defaultValue={valueOrEmpty(childNumber(record, "Tow_Truck_code"))}
          >
            <option value="">Select assist company</option>
            {towTrucks.map((item) => (
              <option key={item.code} value={item.code}>
                {item.name ?? item.code}
                {item.telephone ? ` (${item.telephone})` : ""}
              </option>
            ))}
          </select>
        </div>
        <Field
          id="towing-contact-name"
          label="Contact Person"
          name="towing_contact_person_name"
          maxLength={30}
          defaultValue={childValue(record, "Contact_person_name")}
        />
        <Field
          id="towing-contact-tel"
          label="Contact Telephone"
          name="towing_contact_person_tel"
          maxLength={30}
          defaultValue={childValue(record, "Contact_person_tel")}
        />
        <Field
          id="towing-remarks"
          label="Towing Remarks"
          name="towing_remarks"
          maxLength={50}
          defaultValue={childValue(record, "Remaks")}
        />
      </div>
    </section>
  );
}

const IncidentEditor = renderIncidentEditor;

function renderIncidentEditor({
  record,
  sites,
  notifyLists,
  details,
  lossTypes,
  towTrucks,
}: Readonly<{
  record: CallCentreIncidentRecord;
  sites: CallCentreSiteOption[];
  notifyLists: NotifyListRecord[];
  details: CallCentreEditDetails;
  lossTypes: LossTypeOption[];
  towTrucks: TowTruckOption[];
}>) {
  return (
    <form action={updateCallCentreIncidentAction} className="vehicle-form-stack">
      <input name="cccode" type="hidden" value={record.code} />
      <input name="vmf_code" type="hidden" value={valueOrEmpty(record.vmfCode)} />
      <input name="Incident_type" type="hidden" value={valueOrEmpty(record.incidentType)} />
      <input name="Capture_name" type="hidden" value={valueOrEmpty(record.captureName)} />
      <input name="User_access_code" type="hidden" value={valueOrEmpty(record.userAccessCode)} />
      <input name="Counter" type="hidden" value={valueOrEmpty(record.counter)} />
      <input
        name="child-record-available"
        type="hidden"
        value={
          record.incidentType?.toLowerCase() === "accident"
            ? details.accident
              ? "1"
              : "0"
            : record.incidentType?.toLowerCase() === "loss_theft"
              ? details.loss
                ? "1"
                : "0"
              : record.incidentType?.toLowerCase() === "road_assistance"
                ? details.towing
                  ? "1"
                  : "0"
                : "0"
        }
      />
      <IncidentRecordFields record={record} sites={sites} />
      {record.incidentType?.toLowerCase() === "accident" ? (
        <AccidentChildFields
          record={details.accident}
          sites={sites}
          tableAvailable={details.accidentTableAvailable}
        />
      ) : null}
      {record.incidentType?.toLowerCase() === "loss_theft" ? (
        <LossChildFields
          record={details.loss}
          sites={sites}
          lossTypes={lossTypes}
          tableAvailable={details.lossTableAvailable}
        />
      ) : null}
      {record.incidentType?.toLowerCase() === "road_assistance" ? (
        <TowingChildFields
          record={details.towing}
          sites={sites}
          towTrucks={towTrucks}
          tableAvailable={details.towingTableAvailable}
        />
      ) : null}
      <IncidentClosureFields record={record} notifyLists={notifyLists} />
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href="/call-centre">
          Menu
        </Link>
      </div>
    </form>
  );
}

const IncidentEditPageContent = renderIncidentEditPageContent;

async function renderIncidentEditPageContent({ searchParams }: IncidentEditPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/call-centre/incident/edit" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  if (!hasRole(session.roles, CALL_CENTRE_ROLE))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const referenceNumber = (
    queryValue(query.referenceNumber) ??
    queryValue(query.cccode) ??
    ""
  ).trim();
  const code = positiveInt(referenceNumber);
  const error = queryValue(query.error);
  const updated = queryValue(query.updated) === "1";
  let record: CallCentreIncidentRecord | null = null;
  let sites: CallCentreSiteOption[] = EMPTY_CALL_CENTRE_SITES;
  let notifyLists: NotifyListRecord[] = EMPTY_NOTIFY_LISTS;
  let details: CallCentreEditDetails = EMPTY_EDIT_DETAILS;
  let lossTypes: LossTypeOption[] = [];
  let towTrucks: TowTruckOption[] = [];
  let loadError = "";

  if (code !== null) {
    try {
      [record, sites, notifyLists, details] = await Promise.all([
        getCallCentreIncident(code),
        getCallCentreSites(),
        getNotifyLists(),
        getCallCentreEditDetails(code),
      ]);
      const incidentType = record.incidentType?.toLowerCase();
      if (incidentType === "loss_theft") {
        lossTypes = await getCallCentreLossTypes();
      } else if (incidentType === "road_assistance") {
        towTrucks = await getCallCentreTowTrucks();
      }
    } catch (caughtError) {
      if (caughtError instanceof CallCentreApiError && caughtError.reason === "unauthorized")
        return (
          <main className="page-shell vehicle-page-shell">
            <SessionRecovery returnPath="/call-centre/incident/edit" />
          </main>
        );
      loadError =
        caughtError instanceof CallCentreApiError && caughtError.reason === "not-found"
          ? "The call centre incident was not found."
          : "The call centre service is temporarily unavailable. Please try again.";
    }
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="incident-edit-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Call Centre / Incident Section</p>
            <h1 id="incident-edit-title">Edit / Update an Existing Incident</h1>
            <p>
              Update the full legacy Call_centre record and its type-specific child row without
              dropping fields used by the original workflow.
            </p>
          </div>
          <Link className="button button-secondary" href="/call-centre">
            Call Centre Menu
          </Link>
        </header>
        {error ? (
          <div className="notice notice-error" role="alert">
            {error}
          </div>
        ) : null}
        {updated ? (
          <div className="notice notice-success" role="status">
            Incident GMT {referenceNumber} updated successfully.
          </div>
        ) : null}
        <LookupForm referenceNumber={referenceNumber} />
        {loadError ? (
          <div className="notice notice-error" role="alert">
            {loadError}
          </div>
        ) : null}
        {record?.incidentType?.toLowerCase() === "booking" ? (
          <section className="vehicle-status-card" role="status">
            <p className="eyebrow">Booking incident</p>
            <h2>Continue in the booking workflow</h2>
            <p className="muted-copy">
              The legacy edit path sends Booking incidents to the booking record. The booking page
              remains a separate migration slice.
            </p>
            <Link
              className="button button-primary"
              href={`/call-centre/bookings?gmt=${record.code}`}
            >
              Open booking
            </Link>
          </section>
        ) : null}
        {record && record.incidentType?.toLowerCase() !== "booking" ? (
          <IncidentEditor
            record={record}
            sites={sites}
            notifyLists={notifyLists}
            details={details}
            lossTypes={lossTypes}
            towTrucks={towTrucks}
          />
        ) : null}
      </section>
    </main>
  );
}

export default function IncidentEditPage(props: IncidentEditPageProps) {
  return (
    <StreamedRoute>
      <IncidentEditPageContent {...props} />
    </StreamedRoute>
  );
}
