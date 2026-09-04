import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { saveQueryIncidentAction } from "@/app/call-centre/incident/capture/actions";
import SessionRecovery from "@/app/home/session-recovery";
import {
  CallCentreApiError,
  getCallCentreSites,
  getCallCentreVehicle,
  searchCallCentreVehicles,
  type CallCentreSiteOption,
  type CallCentreVehicleOption,
} from "@/lib/api-call-centre";
import { getNotifyLists, type NotifyListRecord } from "@/lib/api-notify-list";
import { getSession } from "@/lib/session";

const CALL_CENTRE_ROLE = "Call Centre";
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
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
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
      <div className="status-icon status-icon-error" aria-hidden="true">!</div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to capture call centre incidents.</h2>
      <p className="muted-copy">This page requires the Call Centre role.</p>
    </section>
  );
}

function ApiUnavailable({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">!</div>
      <p className="eyebrow">Service unavailable</p>
      <h2>Incident capture could not be opened.</h2>
      <p className="muted-copy">{message}</p>
      <div className="button-row">
        <Link className="button button-primary" href="/call-centre/incident/capture">Try again</Link>
        <Link className="button button-secondary" href="/call-centre">Back to Call Centre</Link>
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
                <option key={type.value} value={type.value}>{type.label}</option>
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
          <button className="button button-primary" type="submit">Find Vehicle</button>
          <Link className="button button-secondary" href="/call-centre">Cancel</Link>
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
        <p className="muted-copy">No vehicle matched “{identifier}”. Check the lookup type and try again.</p>
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
          <p className="eyebrow">{incidentType} · {vehicle.displayText}</p>
          <h2 id="query-form-title">Capture Incident Details</h2>
        </div>
      </div>
      {error ? <p className="form-error" role="alert">{error}</p> : null}
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
                {site.description}{site.departmentNumber ? ` (${site.departmentNumber})` : ""}
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
                <option key={item.code} value={item.code}>{item.description ?? item.email ?? item.code}</option>
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
          <button className="button button-primary" type="submit">Submit</button>
          <Link className="button button-secondary" href="/call-centre/incident/capture">Cancel</Link>
        </div>
      </form>
    </section>
  );
}

export default async function IncidentCapturePage({ searchParams }: IncidentCapturePageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/call-centre/incident/capture" /></main>;
  }

  if (session.status === "unavailable") {
    return <main className="page-shell vehicle-page-shell"><ApiUnavailable message="The sign-in service is temporarily unavailable." /></main>;
  }

  if (!hasRole(session.roles, CALL_CENTRE_ROLE)) {
    return <main className="page-shell vehicle-page-shell"><AccessRestricted /></main>;
  }

  const params = await searchParams;
  const incidentType = getQueryValue(params.incidentType) ?? getQueryValue(params.xinctype) ?? "Query";
  const lookupType = getQueryValue(params.lookupType) ?? "GG";
  const identifier = getQueryValue(params.identifier) ?? getQueryValue(params.xggnum) ?? "";
  const departmentSort = getQueryValue(params.departmentSort) ?? "description";
  const vmfCode = getPositiveInt(getQueryValue(params.vmfCode));
  const error = getQueryValue(params.error) ?? "";
  const saved = getQueryValue(params.saved) === "1";
  const savedCode = getQueryValue(params.code);

  let vehicle: CallCentreVehicleOption | null = null;
  let vehicles: CallCentreVehicleOption[] = [];
  let sites: CallCentreSiteOption[] = [];
  let notifyLists: NotifyListRecord[] = [];
  let loadError = "";

  try {
    if (vmfCode !== null) {
      [vehicle, sites, notifyLists] = await Promise.all([
        getCallCentreVehicle(vmfCode),
        getCallCentreSites(),
        getNotifyLists(),
      ]);
    } else if (identifier.trim()) {
      vehicles = (await searchCallCentreVehicles(identifier.trim())).filter((candidate) => {
        const value = lookupType.toUpperCase() === "GP" ? candidate.registrationNumber : candidate.fleetNumber;
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
          <Link className="button button-secondary" href="/call-centre">Back to Call Centre</Link>
        </header>
        {saved ? (
          <section className="vehicle-status-card" role="status">
            <div className="status-icon status-icon-success" aria-hidden="true">✓</div>
            <p className="eyebrow">Incident captured</p>
            <h2>{savedCode ? `Call Centre reference ${savedCode}` : "The incident was captured successfully."}</h2>
            <p className="muted-copy">The legacy Call_centre business fields were saved.</p>
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
                <p className="muted-copy">The booking form remains a separately inventoried slice and has not been guessed into this Query form.</p>
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
            {incidentType !== "Query" && incidentType !== "Booking" && vehicle ? (
              <section className="vehicle-status-card" role="status">
                <p className="eyebrow">{incidentType}</p>
                <h2>This incident branch is next in the capture migration.</h2>
                <p className="muted-copy">Vehicle selection is preserved. Its type-specific fields and related legacy table writes will be migrated before this branch replaces Blazor.</p>
              </section>
            ) : null}
          </div>
        ) : null}
      </section>
    </main>
  );
}
