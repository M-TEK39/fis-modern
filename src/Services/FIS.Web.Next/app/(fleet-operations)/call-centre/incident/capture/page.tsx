import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import {
  saveAccidentAction,
  saveHiJackAction,
  saveLossAction,
  saveQueryIncidentAction,
  saveRoadAssistanceAction,
} from "@/app/(fleet-operations)/call-centre/incident/capture/actions";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import {
  CallCentreApiError,
  getCallCentreSites,
  getCallCentreTowTrucks,
  getCallCentreLossTypes,
  getCallCentreVehicle,
  searchCallCentreVehicles,
  type CallCentreSiteOption,
  type CallCentreVehicleOption,
  type LossTypeOption,
  type TowTruckOption,
} from "@/lib/api/fleet-operations/api-call-centre";
import { getNotifyLists, type NotifyListRecord } from "@/lib/api/administration/api-notify-list";
import { getSession } from "@/lib/auth/session";

const CALL_CENTRE_ROLE = "Call Centre";
const EMPTY_CALL_CENTRE_SITES: CallCentreSiteOption[] = [];
const EMPTY_NOTIFY_LISTS: NotifyListRecord[] = [];
const EMPTY_TOW_TRUCKS: TowTruckOption[] = [];
const EMPTY_LOSS_TYPES: LossTypeOption[] = [];
const INCIDENT_TYPES = [
  { value: "Query", label: "Query" },
  { value: "Booking", label: "Booking" },
  { value: "Road_Assistance", label: "Road Assistance" },
  { value: "Accident", label: "Accident" },
  { value: "Hi-Jack", label: "Hi-Jack" },
  { value: "Loss_Theft", label: "Loss / Theft" },
] as const;

export type IncidentCapturePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getPositiveInt(value: string | undefined) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function errorMessage(error: unknown) {
  if (error instanceof CallCentreApiError) {
    if (error.reason === "unauthorized") {
      return "Your session has expired. Sign in again before continuing.";
    }

    if (error.reason === "unavailable") {
      return "The call centre service is temporarily unavailable. Please try again.";
    }
  }

  return "The requested call centre data could not be loaded.";
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to capture call centre incidents.</h2>
      <p className="muted-copy">This page requires the Call Centre role.</p>
    </section>
  );
}

function ApiUnavailable({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Service unavailable</p>
      <h2>Incident capture could not be opened.</h2>
      <p className="muted-copy">{message}</p>
      <div className="button-row">
        <Link className="button button-primary" href="/call-centre/incident/capture">
          Try again
        </Link>
        <Link className="button button-secondary" href="/call-centre">
          Back to Call Centre
        </Link>
      </div>
    </section>
  );
}

function IncidentSelector({
  incidentType,
  lookupType,
  identifier,
  departmentSort,
}: Readonly<{
  incidentType: string;
  lookupType: string;
  identifier: string;
  departmentSort: string;
}>) {
  return (
    <section className="vehicle-form-section" aria-labelledby="incident-selector-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Incident section</p>
          <h2 id="incident-selector-title">Start a New Incident</h2>
        </div>
      </div>
      <form action="/call-centre/incident/capture" method="get" className="form-stack">
        <div className="field-grid">
          <div className="field">
            <label htmlFor="incident-type">Incident Type</label>
            <select id="incident-type" name="incidentType" defaultValue={incidentType}>
              {INCIDENT_TYPES.map((type) => (
                <option key={type.value} value={type.value}>
                  {type.label}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="lookup-type">Search By</label>
            <select id="lookup-type" name="lookupType" defaultValue={lookupType}>
              <option value="GG">GG Number</option>
              <option value="GP">Registration Number</option>
            </select>
          </div>
        </div>
        <div className="field">
          <label htmlFor="incident-identifier">GG / Registration Number</label>
          <input
            id="incident-identifier"
            name="identifier"
            maxLength={8}
            defaultValue={identifier}
            required
          />
        </div>
        <div className="field">
          <label htmlFor="department-sort">Site List Order</label>
          <select id="department-sort" name="departmentSort" defaultValue={departmentSort}>
            <option value="description">Description</option>
            <option value="department">Department Number</option>
          </select>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Find Vehicle
          </button>
          <Link className="button button-secondary" href="/call-centre">
            Cancel
          </Link>
        </div>
      </form>
    </section>
  );
}

function VehicleMatches({
  vehicles,
  incidentType,
  lookupType,
  identifier,
  departmentSort,
}: Readonly<{
  vehicles: CallCentreVehicleOption[];
  incidentType: string;
  lookupType: string;
  identifier: string;
  departmentSort: string;
}>) {
  return (
    <section className="vehicle-form-section" aria-labelledby="vehicle-results-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Vehicle lookup</p>
          <h2 id="vehicle-results-title">
            {vehicles.length === 1 ? "Select the vehicle" : `${vehicles.length} vehicles found`}
          </h2>
        </div>
      </div>
      {vehicles.length === 0 ? (
        <p className="muted-copy">
          No vehicle matched “{identifier}”. Check the lookup type and try again.
        </p>
      ) : (
        <div className="vehicle-menu-links">
          {vehicles.map((vehicle) => {
            const params = new URLSearchParams({
              incidentType,
              lookupType,
              identifier,
              departmentSort,
              vmfCode: String(vehicle.vmfCode),
            });
            return (
              <Link
                className="vehicle-menu-link"
                href={`/call-centre/incident/capture?${params.toString()}`}
                key={vehicle.vmfCode}
              >
                {vehicle.displayText}
              </Link>
            );
          })}
        </div>
      )}
    </section>
  );
}

function QueryIncidentForm({
  vehicle,
  sites,
  notifyLists,
  incidentType,
  error,
}: Readonly<{
  vehicle: CallCentreVehicleOption;
  sites: CallCentreSiteOption[];
  notifyLists: NotifyListRecord[];
  incidentType: string;
  error: string;
}>) {
  const selectedSite = sites[0]?.code ?? "";
  return (
    <section className="vehicle-form-section" aria-labelledby="query-form-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">
            {incidentType} · {vehicle.displayText}
          </p>
          <h2 id="query-form-title">Capture Incident Details</h2>
        </div>
      </div>
      {error ? (
        <p className="form-error" role="alert">
          {error}
        </p>
      ) : null}
      <form action={saveQueryIncidentAction} className="form-stack">
        <input name="ccVMF" type="hidden" value={vehicle.vmfCode} />
        <input name="xgg" type="hidden" value={vehicle.fleetNumber ?? ""} />
        <input name="xgp" type="hidden" value={vehicle.registrationNumber ?? ""} />
        <input name="xinctype" type="hidden" value={incidentType} />
        <div className="field-grid">
          <div className="field">
            <label htmlFor="transport-officer-name">Trans Officer Name</label>
            <input id="transport-officer-name" name="xtrsname" maxLength={60} />
          </div>
          <div className="field">
            <label htmlFor="transport-officer-tel">Trans Officer Tel</label>
            <input id="transport-officer-tel" name="xtrstel" maxLength={15} />
          </div>
          <div className="field">
            <label htmlFor="transport-officer-fax">Trans Officer Fax</label>
            <input id="transport-officer-fax" name="xtrsfax" maxLength={15} />
          </div>
          <div className="field">
            <label htmlFor="transport-officer-email">Trans Officer Email</label>
            <input id="transport-officer-email" name="xtrseml" maxLength={30} type="email" />
          </div>
        </div>
        <div className="field">
          <label htmlFor="transport-officer-site">Trans Officer Site</label>
          <select id="transport-officer-site" name="xtrssite" defaultValue={selectedSite}>
            <option value="">Select site</option>
            {sites.map((site) => (
              <option key={site.code} value={site.code}>
                {site.description}
                {site.departmentNumber ? ` (${site.departmentNumber})` : ""}
              </option>
            ))}
          </select>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="caller-name">Caller Name</label>
            <input id="caller-name" name="xcalname" maxLength={30} />
          </div>
          <div className="field">
            <label htmlFor="caller-tel">Caller Cell / Tel</label>
            <input id="caller-tel" name="xcaltel" maxLength={30} />
          </div>
          <div className="field">
            <label htmlFor="caller-fax">Caller Fax</label>
            <input id="caller-fax" name="xcalfax" maxLength={15} />
          </div>
          <div className="field">
            <label htmlFor="caller-email">Caller Email</label>
            <input id="caller-email" name="xcaleml" maxLength={30} type="email" />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="inform-cro">Inform CLO of Change?</label>
            <select id="inform-cro" name="xcro" defaultValue="N">
              <option value="N">No</option>
              <option value="Y">Yes</option>
            </select>
          </div>
          <div className="field">
            <label htmlFor="cro-remarks">Remarks for CLO</label>
            <input id="cro-remarks" name="xcrem" maxLength={60} />
          </div>
        </div>
        <div className="field">
          <label htmlFor="incident-notes">Incident Notes</label>
          <textarea id="incident-notes" name="xirem" maxLength={80} rows={3} />
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="notify-list">Notify Following People</label>
            <select id="notify-list" name="xnotel" defaultValue="">
              <option value="">Select notification list</option>
              {notifyLists.map((item) => (
                <option key={item.code} value={item.code}>
                  {item.description ?? item.email ?? item.code}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="call-closed">Call Closed?</label>
            <select id="call-closed" name="xclosed" defaultValue="Y">
              <option value="Y">Yes</option>
              <option value="N">No</option>
            </select>
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Submit
          </button>
          <Link className="button button-secondary" href="/call-centre/incident/capture">
            Cancel
          </Link>
        </div>
      </form>
    </section>
  );
}

function AccidentIncidentForm({
  vehicle,
  sites,
  notifyLists,
  error,
}: Readonly<{
  vehicle: CallCentreVehicleOption;
  sites: CallCentreSiteOption[];
  notifyLists: NotifyListRecord[];
  error: string;
}>) {
  const today = new Date().toISOString().slice(0, 10);
  const selectedSite = sites[0]?.code ?? "";

  return (
    <section className="vehicle-form-section" aria-labelledby="accident-form-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Accident · {vehicle.displayText}</p>
          <h2 id="accident-form-title">Capture Accident Details</h2>
        </div>
      </div>
      {error ? (
        <p className="form-error" role="alert">
          {error}
        </p>
      ) : null}
      <form action={saveAccidentAction} className="form-stack">
        <input name="ccVMF" type="hidden" value={vehicle.vmfCode} />
        <input name="xgg" type="hidden" value={vehicle.fleetNumber ?? ""} />
        <input name="xgp" type="hidden" value={vehicle.registrationNumber ?? ""} />
        <input name="xinctype" type="hidden" value="Accident" />
        <div className="field-grid">
          <div className="field">
            <label htmlFor="accident-transport-officer-name">Trans Officer Name</label>
            <input id="accident-transport-officer-name" name="xtrsname" maxLength={60} />
          </div>
          <div className="field">
            <label htmlFor="accident-transport-officer-tel">Trans Officer Tel</label>
            <input id="accident-transport-officer-tel" name="xtrstel" maxLength={15} />
          </div>
          <div className="field">
            <label htmlFor="accident-transport-officer-fax">Trans Officer Fax</label>
            <input id="accident-transport-officer-fax" name="xtrsfax" maxLength={15} />
          </div>
          <div className="field">
            <label htmlFor="accident-transport-officer-email">Trans Officer Email</label>
            <input
              id="accident-transport-officer-email"
              name="xtrseml"
              maxLength={30}
              type="email"
            />
          </div>
        </div>
        <div className="field">
          <label htmlFor="accident-transport-officer-site">Trans Officer Site</label>
          <select
            id="accident-transport-officer-site"
            name="xtrssite"
            defaultValue={selectedSite}
            required
          >
            <option value="">Select site</option>
            {sites.map((site) => (
              <option key={site.code} value={site.code}>
                {site.description}
                {site.departmentNumber ? ` (${site.departmentNumber})` : ""}
              </option>
            ))}
          </select>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="accident-caller-name">Caller Name</label>
            <input id="accident-caller-name" name="xcalname" maxLength={40} />
          </div>
          <div className="field">
            <label htmlFor="accident-caller-tel">Caller Cell / Tel</label>
            <input id="accident-caller-tel" name="xcaltel" maxLength={30} />
          </div>
          <div className="field">
            <label htmlFor="accident-caller-fax">Caller Fax</label>
            <input id="accident-caller-fax" name="xcalfax" maxLength={15} />
          </div>
          <div className="field">
            <label htmlFor="accident-caller-email">Caller Email</label>
            <input id="accident-caller-email" name="xcaleml" maxLength={30} type="email" />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="accident-driver-name">Driver Name</label>
            <input id="accident-driver-name" name="xdrvname" maxLength={60} />
          </div>
          <div className="field">
            <label htmlFor="accident-driver-tel">Driver Cell / Tel</label>
            <input id="accident-driver-tel" name="xdrvtel" maxLength={30} />
          </div>
          <div className="field">
            <label htmlFor="accident-driver-persal">Driver Persal</label>
            <input id="accident-driver-persal" name="xdrvperno" maxLength={15} />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="accident-inform-cro">Inform CLO of Change?</label>
            <select id="accident-inform-cro" name="xcro" defaultValue="N">
              <option value="N">No</option>
              <option value="Y">Yes</option>
            </select>
          </div>
          <div className="field">
            <label htmlFor="accident-cro-remarks">Remarks for CLO</label>
            <input id="accident-cro-remarks" name="xcrem" maxLength={60} />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="accident-date">Accident Date</label>
            <input id="accident-date" name="xincdat" type="date" defaultValue={today} required />
          </div>
          <div className="field">
            <label htmlFor="accident-time">Accident Time</label>
            <input id="accident-time" name="xinctime" type="time" />
          </div>
        </div>
        <div className="field">
          <label htmlFor="accident-description">Accident Desc</label>
          <input id="accident-description" name="xincdesc" maxLength={60} />
        </div>
        <div className="field">
          <label htmlFor="accident-damage-description">GG Damage Desc</label>
          <input id="accident-damage-description" name="txtDamage" maxLength={60} />
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="accident-third-party-reg">Private Party Regno</label>
            <input id="accident-third-party-reg" name="txtThregno" maxLength={8} />
          </div>
          <div className="field">
            <label htmlFor="accident-third-party-name">Private Party Name</label>
            <input id="accident-third-party-name" name="txtThname" maxLength={30} />
          </div>
          <div className="field">
            <label htmlFor="accident-third-party-tel">Private Party Cell / Tel</label>
            <input id="accident-third-party-tel" name="txtThtel" maxLength={30} />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="accident-death">Death?</label>
            <select id="accident-death" name="txtDeath" defaultValue="?">
              <option value="?">?</option>
              <option value="N">No</option>
              <option value="Y">Yes</option>
            </select>
          </div>
          <div className="field">
            <label htmlFor="accident-injured">Injured?</label>
            <select id="accident-injured" name="txtInjured" defaultValue="?">
              <option value="?">?</option>
              <option value="N">No</option>
              <option value="Y">Yes</option>
            </select>
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="accident-suburb">Suburb (of Accident)</label>
            <input id="accident-suburb" name="x1town" maxLength={50} />
          </div>
          <div className="field">
            <label htmlFor="accident-town">Town</label>
            <input id="accident-town" name="x2town" maxLength={50} />
          </div>
        </div>
        <div className="field">
          <label htmlFor="accident-street">Street Name</label>
          <input id="accident-street" name="xstreet" maxLength={30} />
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="accident-tow-needed">Need Tow Truck?</label>
            <select id="accident-tow-needed" name="xtowneed" defaultValue="?" required>
              <option value="?">?</option>
              <option value="N">No</option>
              <option value="Y">Yes</option>
            </select>
          </div>
          <div className="field">
            <label htmlFor="accident-notes">Notes</label>
            <input id="accident-notes" name="txtNotes" maxLength={55} />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="accident-notify-list">Notify Following People</label>
            <select id="accident-notify-list" name="xnotc" defaultValue="">
              <option value="">Select notification list</option>
              {notifyLists.map((item) => (
                <option key={item.code} value={item.code}>
                  {item.description ?? item.email ?? item.code}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="accident-call-closed">Call Closed?</label>
            <select id="accident-call-closed" name="xclosed" defaultValue="N">
              <option value="N">No</option>
              <option value="Y">Yes</option>
            </select>
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Submit
          </button>
          <Link className="button button-secondary" href="/call-centre/incident/capture">
            Cancel
          </Link>
        </div>
      </form>
    </section>
  );
}

function LossIncidentForm({
  vehicle,
  sites,
  notifyLists,
  lossTypes,
  error,
}: Readonly<{
  vehicle: CallCentreVehicleOption;
  sites: CallCentreSiteOption[];
  notifyLists: NotifyListRecord[];
  lossTypes: LossTypeOption[];
  error: string;
}>) {
  const today = new Date().toISOString().slice(0, 10);
  const selectedSite = sites[0]?.code ?? "";
  const selectedLossType = lossTypes[0]?.code?.toString() ?? "";

  return (
    <section className="vehicle-form-section" aria-labelledby="loss-form-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Loss / Theft · {vehicle.displayText}</p>
          <h2 id="loss-form-title">Capture Loss Details</h2>
        </div>
      </div>
      {error ? (
        <p className="form-error" role="alert">
          {error}
        </p>
      ) : null}
      <form action={saveLossAction} className="form-stack">
        <input name="ccVMF" type="hidden" value={vehicle.vmfCode} />
        <input name="xgg" type="hidden" value={vehicle.fleetNumber ?? ""} />
        <input name="xgp" type="hidden" value={vehicle.registrationNumber ?? ""} />
        <input name="xinctype" type="hidden" value="Loss_Theft" />
        <div className="field-grid">
          <div className="field">
            <label htmlFor="loss-transport-officer-name">Trans Officer Name</label>
            <input id="loss-transport-officer-name" name="xtrsname" maxLength={60} />
          </div>
          <div className="field">
            <label htmlFor="loss-transport-officer-tel">Trans Officer Tel</label>
            <input id="loss-transport-officer-tel" name="xtrstel" maxLength={15} />
          </div>
          <div className="field">
            <label htmlFor="loss-transport-officer-fax">Trans Officer Fax</label>
            <input id="loss-transport-officer-fax" name="xtrsfax" maxLength={15} />
          </div>
          <div className="field">
            <label htmlFor="loss-transport-officer-email">Trans Officer Email</label>
            <input id="loss-transport-officer-email" name="xtrseml" maxLength={30} type="email" />
          </div>
        </div>
        <div className="field">
          <label htmlFor="loss-transport-officer-site">Trans Officer Site</label>
          <select
            id="loss-transport-officer-site"
            name="xtrssite"
            defaultValue={selectedSite}
            required
          >
            <option value="">Select site</option>
            {sites.map((site) => (
              <option key={site.code} value={site.code}>
                {site.description}
                {site.departmentNumber ? ` (${site.departmentNumber})` : ""}
              </option>
            ))}
          </select>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="loss-caller-name">Caller Name</label>
            <input id="loss-caller-name" name="xcalname" maxLength={40} />
          </div>
          <div className="field">
            <label htmlFor="loss-caller-tel">Caller Cell / Tel</label>
            <input id="loss-caller-tel" name="xcaltel" maxLength={30} />
          </div>
          <div className="field">
            <label htmlFor="loss-caller-fax">Caller Fax</label>
            <input id="loss-caller-fax" name="xcalfax" maxLength={15} />
          </div>
          <div className="field">
            <label htmlFor="loss-caller-email">Caller Email</label>
            <input id="loss-caller-email" name="xcaleml" maxLength={30} type="email" />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="loss-driver-name">Driver Name</label>
            <input id="loss-driver-name" name="xdrvname" maxLength={60} />
          </div>
          <div className="field">
            <label htmlFor="loss-driver-tel">Driver Cell / Tel</label>
            <input id="loss-driver-tel" name="xdrvtel" maxLength={30} />
          </div>
          <div className="field">
            <label htmlFor="loss-driver-persal">Driver Persal</label>
            <input id="loss-driver-persal" name="xdrvperno" maxLength={15} />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="loss-inform-cro">Inform CLO of Change?</label>
            <select id="loss-inform-cro" name="xcro" defaultValue="N">
              <option value="N">No</option>
              <option value="Y">Yes</option>
            </select>
          </div>
          <div className="field">
            <label htmlFor="loss-cro-remarks">Remarks for CLO</label>
            <input id="loss-cro-remarks" name="xcrem" maxLength={60} />
          </div>
        </div>
        <div className="field">
          <label htmlFor="loss-date">Date of Loss</label>
          <input id="loss-date" name="xincdat" type="date" defaultValue={today} required />
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="loss-suburb">Suburb (of Loss)</label>
            <input id="loss-suburb" name="x1town" maxLength={30} />
          </div>
          <div className="field">
            <label htmlFor="loss-town">Town</label>
            <input id="loss-town" name="x2town" maxLength={20} />
          </div>
        </div>
        <div className="field">
          <label htmlFor="loss-street">Street Name</label>
          <input id="loss-street" name="xstreet" maxLength={30} />
        </div>
        <div className="field">
          <label htmlFor="loss-type">Loss Type</label>
          <select id="loss-type" name="xlosst" defaultValue={selectedLossType} required>
            <option value="">Select loss type</option>
            {lossTypes.map((lossType) => (
              <option key={lossType.code} value={lossType.code}>
                {lossType.description}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="loss-description">Description of Loss</label>
          <input id="loss-description" name="xincdesc" maxLength={60} />
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="loss-tow-needed">Need Tow Truck?</label>
            <select id="loss-tow-needed" name="xtowneed" defaultValue="?" required>
              <option value="?">Choose an option</option>
              <option value="N">No</option>
              <option value="Y">Yes</option>
            </select>
          </div>
          <div className="field">
            <label htmlFor="loss-remarks">Remarks</label>
            <input id="loss-remarks" name="xrem" maxLength={50} />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="loss-notify-list">Notify Following People</label>
            <select id="loss-notify-list" name="xnotc" defaultValue="">
              <option value="">Select notification list</option>
              {notifyLists.map((item) => (
                <option key={item.code} value={item.code}>
                  {item.description ?? item.email ?? item.code}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="loss-call-closed">Call Closed?</label>
            <select id="loss-call-closed" name="xclosed" defaultValue="N">
              <option value="N">No</option>
              <option value="Y">Yes</option>
            </select>
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Submit
          </button>
          <Link className="button button-secondary" href="/call-centre/incident/capture">
            Cancel
          </Link>
        </div>
      </form>
    </section>
  );
}

function HiJackIncidentForm({
  vehicle,
  sites,
  notifyLists,
  error,
}: Readonly<{
  vehicle: CallCentreVehicleOption;
  sites: CallCentreSiteOption[];
  notifyLists: NotifyListRecord[];
  error: string;
}>) {
  const today = new Date().toISOString().slice(0, 10);
  const selectedSite = sites[0]?.code ?? "";

  return (
    <section className="vehicle-form-section" aria-labelledby="hijack-form-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Hi-Jack · {vehicle.displayText}</p>
          <h2 id="hijack-form-title">Capture Hi-Jack Details</h2>
        </div>
      </div>
      {error ? (
        <p className="form-error" role="alert">
          {error}
        </p>
      ) : null}
      <form action={saveHiJackAction} className="form-stack">
        <input name="ccVMF" type="hidden" value={vehicle.vmfCode} />
        <input name="xgg" type="hidden" value={vehicle.fleetNumber ?? ""} />
        <input name="xgp" type="hidden" value={vehicle.registrationNumber ?? ""} />
        <input name="xinctype" type="hidden" value="Hi-Jack" />
        <div className="field-grid">
          <div className="field">
            <label htmlFor="hijack-transport-officer-name">Trans Officer Name</label>
            <input id="hijack-transport-officer-name" name="xtrsname" maxLength={30} />
          </div>
          <div className="field">
            <label htmlFor="hijack-transport-officer-tel">Trans Officer Tel</label>
            <input id="hijack-transport-officer-tel" name="xtrstel" maxLength={15} />
          </div>
          <div className="field">
            <label htmlFor="hijack-transport-officer-fax">Trans Officer Fax</label>
            <input id="hijack-transport-officer-fax" name="xtrsfax" maxLength={15} />
          </div>
          <div className="field">
            <label htmlFor="hijack-transport-officer-email">Trans Officer Email</label>
            <input id="hijack-transport-officer-email" name="xtrseml" maxLength={30} type="email" />
          </div>
        </div>
        <div className="field">
          <label htmlFor="hijack-transport-officer-site">Trans Officer Site</label>
          <select
            id="hijack-transport-officer-site"
            name="xtrssite"
            defaultValue={selectedSite}
            required
          >
            <option value="">Select site</option>
            {sites.map((site) => (
              <option key={site.code} value={site.code}>
                {site.description}
                {site.departmentNumber ? ` (${site.departmentNumber})` : ""}
              </option>
            ))}
          </select>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="hijack-caller-name">Caller Name</label>
            <input id="hijack-caller-name" name="xcalname" maxLength={30} />
          </div>
          <div className="field">
            <label htmlFor="hijack-caller-tel">Caller Cell / Tel</label>
            <input id="hijack-caller-tel" name="xcaltel" maxLength={30} />
          </div>
          <div className="field">
            <label htmlFor="hijack-caller-fax">Caller Fax</label>
            <input id="hijack-caller-fax" name="xcalfax" maxLength={15} />
          </div>
          <div className="field">
            <label htmlFor="hijack-caller-email">Caller Email</label>
            <input id="hijack-caller-email" name="xcaleml" maxLength={30} type="email" />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="hijack-driver-name">Driver Name</label>
            <input id="hijack-driver-name" name="xdrvname" maxLength={60} />
          </div>
          <div className="field">
            <label htmlFor="hijack-driver-tel">Driver Cell / Tel</label>
            <input id="hijack-driver-tel" name="xdrvtel" maxLength={30} />
          </div>
          <div className="field">
            <label htmlFor="hijack-driver-persal">Driver Persal</label>
            <input id="hijack-driver-persal" name="xdrvperno" maxLength={15} />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="hijack-inform-cro">Inform CLO of Change?</label>
            <select id="hijack-inform-cro" name="xcro" defaultValue="N">
              <option value="N">No</option>
              <option value="Y">Yes</option>
            </select>
          </div>
          <div className="field">
            <label htmlFor="hijack-cro-remarks">Remarks for CLO</label>
            <input id="hijack-cro-remarks" name="xcrem" maxLength={60} />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="hijack-date">Hi-Jack Date</label>
            <input id="hijack-date" name="xincdat" type="date" defaultValue={today} required />
          </div>
          <div className="field">
            <label htmlFor="hijack-time">Hi-Jack Time</label>
            <input id="hijack-time" name="xinctime" type="time" />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="hijack-suburb">Suburb</label>
            <input id="hijack-suburb" name="x1town" maxLength={30} />
          </div>
          <div className="field">
            <label htmlFor="hijack-town">Town</label>
            <input id="hijack-town" name="x2town" maxLength={20} />
          </div>
        </div>
        <div className="field">
          <label htmlFor="hijack-street">Street Name</label>
          <input id="hijack-street" name="xstreet" maxLength={30} />
        </div>
        <div className="field">
          <label htmlFor="hijack-description">Description of Hi-Jack</label>
          <input id="hijack-description" name="xincdesc" maxLength={60} />
        </div>
        <div className="field">
          <label htmlFor="hijack-remarks">Remarks</label>
          <input id="hijack-remarks" name="xrem" maxLength={80} />
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="hijack-notify-list">Notify Following People</label>
            <select id="hijack-notify-list" name="xnotc" defaultValue="">
              <option value="">Select notification list</option>
              {notifyLists.map((item) => (
                <option key={item.code} value={item.code}>
                  {item.description ?? item.email ?? item.code}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="hijack-call-closed">Call Closed?</label>
            <select id="hijack-call-closed" name="xclosed" defaultValue="N">
              <option value="N">No</option>
              <option value="Y">Yes</option>
            </select>
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Submit
          </button>
          <Link className="button button-secondary" href="/call-centre/incident/capture">
            Cancel
          </Link>
        </div>
      </form>
    </section>
  );
}

function RoadAssistanceForm({
  vehicle,
  sites,
  notifyLists,
  towTrucks,
  error,
}: Readonly<{
  vehicle: CallCentreVehicleOption;
  sites: CallCentreSiteOption[];
  notifyLists: NotifyListRecord[];
  towTrucks: TowTruckOption[];
  error: string;
}>) {
  const today = new Date().toISOString().slice(0, 10);
  const selectedSite = sites[0]?.code ?? "";

  return (
    <section className="vehicle-form-section" aria-labelledby="road-form-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Road Assistance · {vehicle.displayText}</p>
          <h2 id="road-form-title">Capture Road Assistance Details</h2>
        </div>
      </div>
      {error ? (
        <p className="form-error" role="alert">
          {error}
        </p>
      ) : null}
      <form action={saveRoadAssistanceAction} className="form-stack">
        <input name="ccVMF" type="hidden" value={vehicle.vmfCode} />
        <input name="xgg" type="hidden" value={vehicle.fleetNumber ?? ""} />
        <input name="xgp" type="hidden" value={vehicle.registrationNumber ?? ""} />
        <input name="xinctype" type="hidden" value="Road_Assistance" />
        <div className="field-grid">
          <div className="field">
            <label htmlFor="road-transport-officer-name">Trans Officer Name</label>
            <input id="road-transport-officer-name" name="xtrsname" maxLength={60} />
          </div>
          <div className="field">
            <label htmlFor="road-transport-officer-tel">Trans Officer Tel</label>
            <input id="road-transport-officer-tel" name="xtrstel" maxLength={15} />
          </div>
          <div className="field">
            <label htmlFor="road-transport-officer-fax">Trans Officer Fax</label>
            <input id="road-transport-officer-fax" name="xtrsfax" maxLength={15} />
          </div>
          <div className="field">
            <label htmlFor="road-transport-officer-email">Trans Officer Email</label>
            <input id="road-transport-officer-email" name="xtrseml" maxLength={30} type="email" />
          </div>
        </div>
        <div className="field">
          <label htmlFor="road-transport-officer-site">Trans Officer Site</label>
          <select
            id="road-transport-officer-site"
            name="xtrssite"
            defaultValue={selectedSite}
            required
          >
            <option value="">Select site</option>
            {sites.map((site) => (
              <option key={site.code} value={site.code}>
                {site.description}
                {site.departmentNumber ? ` (${site.departmentNumber})` : ""}
              </option>
            ))}
          </select>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="road-caller-name">Caller Name</label>
            <input id="road-caller-name" name="xcalname" maxLength={30} />
          </div>
          <div className="field">
            <label htmlFor="road-caller-tel">Caller Cell / Tel</label>
            <input id="road-caller-tel" name="xcaltel" maxLength={30} />
          </div>
          <div className="field">
            <label htmlFor="road-caller-fax">Caller Fax</label>
            <input id="road-caller-fax" name="xcalfax" maxLength={15} />
          </div>
          <div className="field">
            <label htmlFor="road-caller-email">Caller Email</label>
            <input id="road-caller-email" name="xcaleml" maxLength={30} type="email" />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="road-driver-name">Driver Name</label>
            <input id="road-driver-name" name="xdrvname" maxLength={60} />
          </div>
          <div className="field">
            <label htmlFor="road-driver-tel">Driver Cell / Tel</label>
            <input id="road-driver-tel" name="xdrvtel" maxLength={30} />
          </div>
          <div className="field">
            <label htmlFor="road-driver-persal">Driver Persal</label>
            <input id="road-driver-persal" name="xdrvperno" maxLength={15} />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="road-inform-cro">Inform CLO of Change?</label>
            <select id="road-inform-cro" name="xcro" defaultValue="N">
              <option value="N">No</option>
              <option value="Y">Yes</option>
            </select>
          </div>
          <div className="field">
            <label htmlFor="road-cro-remarks">Remarks for CLO</label>
            <input id="road-cro-remarks" name="xcrem" maxLength={60} />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="road-incident-date">Incident Date</label>
            <input
              id="road-incident-date"
              name="xincdat"
              type="date"
              defaultValue={today}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="road-incident-time">Incident Time</label>
            <input id="road-incident-time" name="xinctime" type="time" required />
          </div>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="road-town">Town</label>
            <input id="road-town" name="x2town" maxLength={20} />
          </div>
          <div className="field">
            <label htmlFor="road-suburb">Suburb</label>
            <input id="road-suburb" name="x1town" maxLength={30} />
          </div>
        </div>
        <div className="field">
          <label htmlFor="road-street">Street Name</label>
          <input id="road-street" name="xstreet" maxLength={30} />
        </div>
        <div className="field">
          <label htmlFor="road-vehicle-problem">Vehicle Problem</label>
          <input id="road-vehicle-problem" name="xincdesc" maxLength={60} />
        </div>
        <div className="field">
          <label htmlFor="road-tow-truck">Assist Company Name</label>
          <select id="road-tow-truck" name="xtruckcod" defaultValue="">
            <option value="">Select assistance company</option>
            {towTrucks.map((towTruck) => (
              <option key={towTruck.code} value={towTruck.code}>
                {towTruck.name ?? `Company ${towTruck.code}`}
                {towTruck.telephone ? ` (${towTruck.telephone})` : ""}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="road-towing-remarks">Remarks (e.g. Keys, Contact info)</label>
          <input id="road-towing-remarks" name="xrem" maxLength={50} />
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="road-notify-list">Notify Following People</label>
            <select id="road-notify-list" name="xnotc" defaultValue="">
              <option value="">Select notification list</option>
              {notifyLists.map((item) => (
                <option key={item.code} value={item.code}>
                  {item.description ?? item.email ?? item.code}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="road-call-closed">Call Closed?</label>
            <select id="road-call-closed" name="xclosed" defaultValue="N">
              <option value="N">No</option>
              <option value="Y">Yes</option>
            </select>
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Submit
          </button>
          <Link className="button button-secondary" href="/call-centre/incident/capture">
            Cancel
          </Link>
        </div>
      </form>
    </section>
  );
}

const IncidentCapturePageContent = renderIncidentCapturePageContent;

async function renderIncidentCapturePageContent({ searchParams }: IncidentCapturePageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/call-centre/incident/capture" />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable message="The sign-in service is temporarily unavailable." />
      </main>
    );
  }

  if (!hasRole(session.roles, CALL_CENTRE_ROLE)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  const params = await searchParams;
  const incidentType =
    getQueryValue(params.incidentType) ?? getQueryValue(params.xinctype) ?? "Query";
  const lookupType = getQueryValue(params.lookupType) ?? "GG";
  const identifier = getQueryValue(params.identifier) ?? getQueryValue(params.xggnum) ?? "";
  const departmentSort = getQueryValue(params.departmentSort) ?? "description";
  const vmfCode = getPositiveInt(getQueryValue(params.vmfCode));
  const error = getQueryValue(params.error) ?? "";
  const saved = getQueryValue(params.saved) === "1";
  const savedCode = getQueryValue(params.code);

  if (incidentType === "Booking") {
    redirect("/CallCentre/Bookings/Booking_1G.aspx?xinctype=Booking");
  }

  let vehicle: CallCentreVehicleOption | null = null;
  let vehicles: CallCentreVehicleOption[] = [];
  let sites: CallCentreSiteOption[] = EMPTY_CALL_CENTRE_SITES;
  let notifyLists: NotifyListRecord[] = EMPTY_NOTIFY_LISTS;
  let towTrucks: TowTruckOption[] = EMPTY_TOW_TRUCKS;
  let lossTypes: LossTypeOption[] = EMPTY_LOSS_TYPES;
  let loadError = "";

  try {
    if (vmfCode !== null) {
      [vehicle, sites, notifyLists, towTrucks, lossTypes] = await Promise.all([
        getCallCentreVehicle(vmfCode),
        getCallCentreSites(),
        getNotifyLists(),
        incidentType === "Road_Assistance" ? getCallCentreTowTrucks() : Promise.resolve([]),
        incidentType === "Loss_Theft" ? getCallCentreLossTypes() : Promise.resolve([]),
      ]);
    } else if (identifier.trim()) {
      vehicles = (await searchCallCentreVehicles(identifier.trim())).filter((candidate) => {
        const value =
          lookupType.toUpperCase() === "GP" ? candidate.registrationNumber : candidate.fleetNumber;
        return value?.toLowerCase() === identifier.trim().toLowerCase();
      });
    }
  } catch (caughtError) {
    loadError = errorMessage(caughtError);
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="incident-capture-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Call Centre / Incident Section</p>
            <h1 id="incident-capture-title">Capture a New Incident</h1>
            <p>Record the same incident details used by the legacy Call Centre workflow.</p>
          </div>
          <Link className="button button-secondary" href="/call-centre">
            Back to Call Centre
          </Link>
        </header>
        {saved ? (
          <section className="vehicle-status-card" role="status">
            <div className="status-icon status-icon-success" aria-hidden="true">
              ✓
            </div>
            <p className="eyebrow">Incident captured</p>
            <h2>
              {savedCode
                ? `Call Centre reference ${savedCode}`
                : "The incident was captured successfully."}
            </h2>
            <p className="muted-copy">
              {incidentType === "Road_Assistance"
                ? "The legacy Call_centre and Towing business fields were saved."
                : incidentType === "Accident"
                  ? "The legacy Call_centre and Accident business fields were saved."
                  : incidentType === "Hi-Jack"
                    ? "The legacy Call_centre Hi-Jack business fields were saved."
                    : incidentType === "Loss_Theft"
                      ? "The legacy Call_centre and Losses business fields were saved."
                      : "The legacy Call_centre business fields were saved."}
            </p>
          </section>
        ) : null}
        {loadError ? <ApiUnavailable message={loadError} /> : null}
        {!loadError && !saved ? (
          <div className="vehicle-form-stack">
            <IncidentSelector
              incidentType={incidentType}
              lookupType={lookupType}
              identifier={identifier}
              departmentSort={departmentSort}
            />
            {incidentType === "Booking" && identifier ? (
              <section className="vehicle-status-card" role="status">
                <p className="eyebrow">Booking incident</p>
                <h2>Booking capture is a separate legacy workflow.</h2>
                <p className="muted-copy">
                  The booking form remains a separately inventoried slice and has not been guessed
                  into this Query form.
                </p>
              </section>
            ) : null}
            {incidentType !== "Booking" && identifier && vmfCode === null ? (
              <VehicleMatches
                vehicles={vehicles}
                incidentType={incidentType}
                lookupType={lookupType}
                identifier={identifier}
                departmentSort={departmentSort}
              />
            ) : null}
            {incidentType === "Query" && vehicle ? (
              <QueryIncidentForm
                vehicle={vehicle}
                sites={sites}
                notifyLists={notifyLists}
                incidentType={incidentType}
                error={error}
              />
            ) : null}
            {incidentType === "Accident" && vehicle ? (
              <AccidentIncidentForm
                vehicle={vehicle}
                sites={sites}
                notifyLists={notifyLists}
                error={error}
              />
            ) : null}
            {incidentType === "Road_Assistance" && vehicle ? (
              <RoadAssistanceForm
                vehicle={vehicle}
                sites={sites}
                notifyLists={notifyLists}
                towTrucks={towTrucks}
                error={error}
              />
            ) : null}
            {incidentType === "Hi-Jack" && vehicle ? (
              <HiJackIncidentForm
                vehicle={vehicle}
                sites={sites}
                notifyLists={notifyLists}
                error={error}
              />
            ) : null}
            {incidentType === "Loss_Theft" && vehicle ? (
              <LossIncidentForm
                vehicle={vehicle}
                sites={sites}
                notifyLists={notifyLists}
                lossTypes={lossTypes}
                error={error}
              />
            ) : null}
            {incidentType !== "Query" &&
            incidentType !== "Booking" &&
            incidentType !== "Accident" &&
            incidentType !== "Road_Assistance" &&
            incidentType !== "Hi-Jack" &&
            incidentType !== "Loss_Theft" &&
            vehicle ? (
              <section className="vehicle-status-card" role="status">
                <p className="eyebrow">{incidentType}</p>
                <h2>This incident branch is next in the capture migration.</h2>
                <p className="muted-copy">
                  Vehicle selection is preserved. This capture branch is being completed in Next.js
                  with the established type-specific fields and table writes.
                </p>
              </section>
            ) : null}
          </div>
        ) : null}
      </section>
    </main>
  );
}

export default function IncidentCapturePage(props: IncidentCapturePageProps) {
  return (
    <StreamedRoute>
      <IncidentCapturePageContent {...props} />
    </StreamedRoute>
  );
}
