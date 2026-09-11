import type {
  CallCentreIncidentRecord,
  CallCentreSiteOption,
} from "@/lib/api/fleet-operations/api-call-centre";
import type { NotifyListRecord } from "@/lib/api/administration/api-notify-list";

function valueOrEmpty(value: string | number | null | undefined) {
  return value === null || value === undefined ? "" : String(value);
}

function valueOrDash(value: string | number | null | undefined) {
  const text = valueOrEmpty(value);
  return text.trim() ? text : "-";
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

export function IncidentRecordFields({
  record,
  sites,
}: Readonly<{ record: CallCentreIncidentRecord; sites: CallCentreSiteOption[] }>) {
  return (
    <>
      <section className="vehicle-form-section" aria-labelledby="incident-context-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">GMT {record.code}</p>
            <h2 id="incident-context-title">{valueOrDash(record.incidentType)} incident</h2>
          </div>
        </div>
        <div className="field-grid">
          <p className="form-hint">
            GG / VMF: {valueOrDash(record.ggNumber)} / {valueOrDash(record.vmfCode)}
          </p>
          <p className="form-hint">Captured by: {valueOrDash(record.captureName)}</p>
        </div>
      </section>
      <section className="vehicle-form-section" aria-labelledby="call-details-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy Call_centre fields</p>
            <h2 id="call-details-title">Call details</h2>
          </div>
        </div>
        <div className="field-grid">
          <Field
            id="call-date"
            label="Call Date"
            name="Call_date"
            type="date"
            defaultValue={dateInputValue(record.callDate)}
          />
          <Field
            id="call-time"
            label="Call Time"
            name="Call_time"
            type="time"
            defaultValue={timeInputValue(record.callTime)}
          />
          <Field
            id="caller-name"
            label="Caller Name"
            name="Caller_name"
            maxLength={30}
            defaultValue={valueOrEmpty(record.callerName)}
          />
          <Field
            id="caller-tel"
            label="Caller Telephone"
            name="Caller_tel"
            maxLength={30}
            defaultValue={valueOrEmpty(record.callerTel)}
          />
          <Field
            id="caller-fax"
            label="Caller Fax"
            name="Caller_fax"
            maxLength={30}
            defaultValue={valueOrEmpty(record.callerFax)}
          />
          <Field
            id="caller-email"
            label="Caller Email"
            name="Caller_email"
            maxLength={240}
            defaultValue={valueOrEmpty(record.callerEmail)}
          />
        </div>
      </section>
      <section className="vehicle-form-section" aria-labelledby="driver-details-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy driver fields</p>
            <h2 id="driver-details-title">Driver details</h2>
          </div>
        </div>
        <div className="field-grid">
          <Field
            id="driver-name"
            label="Driver Name"
            name="Driver_name"
            maxLength={30}
            defaultValue={valueOrEmpty(record.driverName)}
          />
          <Field
            id="driver-tel"
            label="Driver Telephone"
            name="Driver_tel"
            maxLength={30}
            defaultValue={valueOrEmpty(record.driverTel)}
          />
          <Field
            id="driver-cell"
            label="Driver Cell"
            name="Driver_cell"
            maxLength={30}
            defaultValue={valueOrEmpty(record.driverCell)}
          />
          <Field
            id="driver-fax"
            label="Driver Fax"
            name="Driver_fax"
            maxLength={30}
            defaultValue={valueOrEmpty(record.driverFax)}
          />
          <Field
            id="driver-email"
            label="Driver Email"
            name="Driver_email"
            maxLength={240}
            defaultValue={valueOrEmpty(record.driverEmail)}
          />
          <Field
            id="driver-persal"
            label="Driver Persal Number"
            name="Driver_persalno"
            maxLength={15}
            defaultValue={valueOrEmpty(record.driverPersalNumber)}
          />
          <Field
            id="driver-licence"
            label="Driver Licence Number"
            name="Driver_Licno"
            maxLength={30}
            defaultValue={valueOrEmpty(record.driverLicenceNumber)}
          />
          <Field
            id="driver-base-station"
            label="Driver Base Station"
            name="Driver_base_station"
            maxLength={30}
            defaultValue={valueOrEmpty(record.driverBaseStation)}
          />
          <SiteSelect
            id="driver-site"
            label="Driver Site"
            name="Driver_Site"
            value={record.driverSite}
            sites={sites}
          />
        </div>
      </section>
      <section className="vehicle-form-section" aria-labelledby="transport-details-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy transport officer fields</p>
            <h2 id="transport-details-title">Transport officer</h2>
          </div>
        </div>
        <div className="field-grid">
          <Field
            id="transport-name"
            label="Transport Officer Name"
            name="TrOfficer_name"
            maxLength={30}
            defaultValue={valueOrEmpty(record.transportOfficerName)}
          />
          <Field
            id="transport-tel"
            label="Transport Officer Telephone"
            name="TrOfficer_tel"
            maxLength={30}
            defaultValue={valueOrEmpty(record.transportOfficerTel)}
          />
          <Field
            id="transport-fax"
            label="Transport Officer Fax"
            name="TrOfficer_fax"
            maxLength={30}
            defaultValue={valueOrEmpty(record.transportOfficerFax)}
          />
          <Field
            id="transport-email"
            label="Transport Officer Email"
            name="TrOfficer_email"
            maxLength={240}
            defaultValue={valueOrEmpty(record.transportOfficerEmail)}
          />
          <SiteSelect
            id="transport-site"
            label="Transport Officer Site"
            name="TrOfficer_Site"
            value={record.transportOfficerSite}
            sites={sites}
          />
        </div>
      </section>
      <section className="vehicle-form-section" aria-labelledby="incident-details-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy incident fields</p>
            <h2 id="incident-details-title">Incident details</h2>
          </div>
        </div>
        <div className="field-grid">
          <Field
            id="incident-date"
            label="Incident Date"
            name="Incident_date"
            type="date"
            defaultValue={dateInputValue(record.incidentDate)}
          />
          <Field
            id="incident-time"
            label="Incident Time"
            name="Incident_time"
            type="time"
            defaultValue={timeInputValue(record.incidentTime)}
          />
          <Field
            id="incident-town"
            label="Area - Suburb / Town"
            name="Incident_town"
            maxLength={50}
            defaultValue={valueOrEmpty(record.incidentTown)}
          />
          <Field
            id="incident-street"
            label="Place - Street Name"
            name="Incident_street"
            maxLength={30}
            defaultValue={valueOrEmpty(record.incidentStreet)}
          />
          <Field
            id="incident-description"
            label="Incident Description"
            name="Incident_Desc"
            maxLength={60}
            defaultValue={valueOrEmpty(record.incidentDescription)}
          />
        </div>
      </section>
    </>
  );
}

export function IncidentClosureFields({
  record,
  notifyLists,
}: Readonly<{ record: CallCentreIncidentRecord; notifyLists: NotifyListRecord[] }>) {
  return (
    <section className="vehicle-form-section" aria-labelledby="closure-details-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Legacy notification fields</p>
          <h2 id="closure-details-title">Notifications and closure</h2>
        </div>
      </div>
      <div className="field-grid">
        <ChoiceSelect
          id="inform-cro"
          label="Inform CLO"
          name="Inform_CRO"
          value={record.croNotification}
          choices={["Y", "N"]}
        />
        <ChoiceSelect
          id="call-closed"
          label="Call Closed"
          name="call_closed"
          value={record.callClosed}
          choices={["Y", "N"]}
        />
        <div className="field">
          <label htmlFor="notify-list">Notification List</label>
          <select
            id="notify-list"
            name="Notify_list_code"
            defaultValue={valueOrEmpty(record.notifyListCode)}
          >
            <option value="">Select notification list</option>
            {notifyLists.map((item) => (
              <option key={item.code} value={item.code}>
                {item.description ?? item.email ?? item.code}
              </option>
            ))}
          </select>
        </div>
        <Field
          id="cro-remarks"
          label="CLO Remarks"
          name="CRO_Remarks"
          maxLength={60}
          defaultValue={valueOrEmpty(record.croRemarks)}
        />
        <Field
          id="incident-remarks"
          label="Incident Remarks"
          name="Incident_Remarks"
          maxLength={80}
          defaultValue={valueOrEmpty(record.incidentRemarks)}
        />
      </div>
    </section>
  );
}
