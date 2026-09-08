import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { deleteClearanceAction, saveClearanceAction } from "@/app/clearance/entry/actions";
import {
  ClearanceApiError,
  getClearance,
  getClearanceVehicle,
  getClearancesForVehicle,
  getMerchants,
  lookupClearanceVehicle,
  type ClearanceRecord,
  type ClearanceVehicle,
  type MerchantRecord,
} from "@/lib/api-clearance";
import { getSession } from "@/lib/session";

const CLEARANCE_ROLE = "Clearance";

export type ClearanceEntryPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  forcedAction?: "edit" | "delete";
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getQueryInt(value: string | undefined) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function dateInputValue(value: string | null | undefined) {
  return value?.slice(0, 10) || new Date().toISOString().slice(0, 10);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function VehicleSearch({
  searchType,
  searchTerm,
}: Readonly<{ searchType: "GG" | "GP"; searchTerm: string }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <fieldset className="vehicle-search-options">
        <legend>Search by</legend>
        <label className="vehicle-checkbox-label">
          <input type="radio" name="type" value="GG" defaultChecked={searchType === "GG"} /> GG
        </label>
        <label className="vehicle-checkbox-label">
          <input type="radio" name="type" value="GP" defaultChecked={searchType === "GP"} /> GP
        </label>
      </fieldset>
      <div className="vehicle-search-row">
        <label className="sr-only" htmlFor="clearance-vehicle-search">
          {searchType === "GG" ? "GG Number" : "GP Number"}
        </label>
        <input
          className="vehicle-search"
          id="clearance-vehicle-search"
          name="q"
          maxLength={8}
          placeholder={searchType === "GG" ? "Enter GG number" : "Enter GP number"}
          defaultValue={searchTerm}
        />
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href="/clearance">
          Menu
        </Link>
      </div>
    </form>
  );
}

function ClearanceForm({
  vehicle,
  merchants,
  record,
}: Readonly<{
  vehicle: ClearanceVehicle;
  merchants: MerchantRecord[];
  record: ClearanceRecord | null;
}>) {
  const isEdit = record !== null;

  return (
    <form className="vehicle-status-maintenance-panel" action={saveClearanceAction}>
      <input name="returnPath" type="hidden" value="/clearance/entry" />
      <input name="vmfCode" type="hidden" value={vehicle.vmfCode} />
      {record ? <input name="clearanceCode" type="hidden" value={record.clearanceCode} /> : null}
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{isEdit ? "Existing record" : "New record"}</p>
          <h2>
            {isEdit ? "Edit Clearance" : `Add Clearance for ${valueOrDash(vehicle.fleetNumber)}`}
          </h2>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="clearance-date">
            Date
          </label>
          <input
            className="form-input"
            id="clearance-date"
            name="clearanceDate"
            type="date"
            defaultValue={dateInputValue(record?.clearanceDate)}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="clearance-number">
            Clearance Number
          </label>
          <input
            className="form-input"
            id="clearance-number"
            name="clearanceNumber"
            type="number"
            min="0"
            defaultValue={record?.clearanceNumber ?? ""}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="clearance-merchant">
            Merchant
          </label>
          <select
            className="form-select"
            id="clearance-merchant"
            name="merchantCode"
            defaultValue={record?.merchantCode ?? ""}
          >
            <option value="">Select...</option>
            {merchants.map((merchant) => (
              <option key={merchant.merchantCode} value={merchant.merchantCode}>
                {valueOrDash(merchant.merchantName)}
              </option>
            ))}
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="clearance-amount">
            Amount
          </label>
          <input
            className="form-input"
            id="clearance-amount"
            name="amount"
            type="number"
            min="0"
            step="0.01"
            defaultValue={record?.amount ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="clearance-kilos">
            Clearance Kilos
          </label>
          <input
            className="form-input"
            id="clearance-kilos"
            name="kilos"
            type="number"
            min="0"
            step="1"
            defaultValue={record?.kilos ?? ""}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="clearance-comment">
            Comment
          </label>
          <textarea
            className="form-textarea"
            id="clearance-comment"
            name="comment"
            maxLength={80}
            rows={3}
            defaultValue={record?.comment ?? ""}
            required
          />
        </div>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          {isEdit ? "Update" : "Save"}
        </button>
        <Link className="button button-secondary" href="/clearance/entry">
          Reset
        </Link>
      </div>
    </form>
  );
}

function ClearanceHistory({
  records,
  vehicle,
}: Readonly<{ records: ClearanceRecord[]; vehicle: ClearanceVehicle }>) {
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="clearance-history-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">
            {valueOrDash(vehicle.fleetNumber)} / {valueOrDash(vehicle.registrationNumber)}
          </p>
          <h2 id="clearance-history-title">Previous Clearance Records</h2>
        </div>
      </div>
      {records.length === 0 ? (
        <p className="muted-copy">No previous records found.</p>
      ) : (
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Clearance records for selected vehicle</caption>
            <thead>
              <tr>
                <th scope="col">Comment</th>
                <th scope="col">Date</th>
                <th scope="col">Number</th>
                <th scope="col">Action</th>
              </tr>
            </thead>
            <tbody>
              {records.map((record) => (
                <tr key={record.clearanceCode}>
                  <td>{valueOrDash(record.comment)}</td>
                  <td>{formatDate(record.clearanceDate)}</td>
                  <td>{valueOrDash(record.clearanceNumber)}</td>
                  <td>
                    <div className="button-row">
                      <Link
                        className="button button-secondary button-small"
                        href={`/clearance/entry?code=${record.clearanceCode}&vmfCode=${vehicle.vmfCode}`}
                      >
                        Mod
                      </Link>
                      <Link
                        className="button button-danger button-small"
                        href={`/clearance/entry?deleteCode=${record.clearanceCode}&vmfCode=${vehicle.vmfCode}`}
                      >
                        Del
                      </Link>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function DeleteConfirmation({
  record,
  vehicle,
}: Readonly<{ record: ClearanceRecord; vehicle: ClearanceVehicle }>) {
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="clearance-delete-title">
      <p className="eyebrow">Confirm action</p>
      <h2 id="clearance-delete-title">Delete Clearance {valueOrDash(record.clearanceNumber)}?</h2>
      <p>
        This removes the clearance record for {valueOrDash(vehicle.fleetNumber)} dated{" "}
        {formatDate(record.clearanceDate)}.
      </p>
      <form action={deleteClearanceAction}>
        <input
          name="returnPath"
          type="hidden"
          value={`/clearance/entry?vmfCode=${vehicle.vmfCode}`}
        />
        <input name="clearanceCode" type="hidden" value={record.clearanceCode} />
        <input name="vmfCode" type="hidden" value={vehicle.vmfCode} />
        <div className="button-row">
          <button className="button button-danger" type="submit">
            Delete
          </button>
          <Link
            className="button button-secondary"
            href={`/clearance/entry?vmfCode=${vehicle.vmfCode}`}
          >
            Cancel
          </Link>
        </div>
      </form>
    </section>
  );
}

function Message({ message, success }: Readonly<{ message: string; success?: boolean }>) {
  return (
    <div className={success ? "notice notice-success" : "notice notice-error"} role="status">
      {message}
    </div>
  );
}

function NotFoundState({ searchTerm }: Readonly<{ searchTerm: string }>) {
  return (
    <div className="vehicle-empty-state">
      <p className="eyebrow">Vehicle not found</p>
      <p>
        {searchTerm
          ? `No vehicle matched “${searchTerm}”.`
          : "The requested vehicle or clearance was not found."}
      </p>
    </div>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>Clearance information could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/clearance/entry">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

export default async function ClearanceEntryPage({
  searchParams,
  forcedAction,
  routePath = "/clearance/entry",
}: ClearanceEntryPageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }

  if (!hasRole(session.roles, CLEARANCE_ROLE)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to maintain clearance records.</h2>
        </section>
      </main>
    );
  }

  const query = await searchParams;
  const searchType =
    getQueryValue(query.type) === "GP" || getQueryValue(query.Radio1) === "Radiogp" ? "GP" : "GG";
  const searchTerm = (getQueryValue(query.q) ?? getQueryValue(query.txtGGNum) ?? "").trim();
  const code = getQueryInt(
    getQueryValue(query.code) ?? getQueryValue(query.Code) ?? getQueryValue(query.deleteCode),
  );
  const vmfCode = getQueryInt(getQueryValue(query.vmfCode) ?? getQueryValue(query.vmf_code));
  const action =
    forcedAction ?? (getQueryValue(query.deleteCode) ? "delete" : code ? "edit" : null);

  let vehicle: ClearanceVehicle | null = null;
  let records: ClearanceRecord[] = [];
  let merchants: MerchantRecord[] = [];
  let record: ClearanceRecord | null = null;

  try {
    if (code && action) {
      record = await getClearance(code);
      [vehicle, records, merchants] = await Promise.all([
        getClearanceVehicle(record.vmfCode),
        getClearancesForVehicle(record.vmfCode),
        getMerchants(),
      ]);
    } else if (vmfCode) {
      [vehicle, records, merchants] = await Promise.all([
        getClearanceVehicle(vmfCode),
        getClearancesForVehicle(vmfCode),
        getMerchants(),
      ]);
    } else if (searchTerm) {
      try {
        vehicle = await lookupClearanceVehicle(searchTerm);
      } catch (error) {
        if (!(error instanceof ClearanceApiError && error.reason === "not-found")) {
          throw error;
        }
      }

      if (vehicle) {
        [records, merchants] = await Promise.all([
          getClearancesForVehicle(vehicle.vmfCode),
          getMerchants(),
        ]);
      }
    }
  } catch (error) {
    if (error instanceof ClearanceApiError && error.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    }

    if (error instanceof ClearanceApiError && error.reason === "not-found") {
      vehicle = null;
    } else {
      console.error(
        "FIS clearance request failed",
        error instanceof Error ? error.message : "unknown error",
      );
      return (
        <main className="page-shell vehicle-page-shell">
          <ApiUnavailable />
        </main>
      );
    }
  }

  const saved = getQueryValue(query.saved) === "1";
  const updated = getQueryValue(query.updated) === "1";
  const deleted = getQueryValue(query.deleted) === "1";
  const errorMessage = getQueryValue(query.error);

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="clearance-entry-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Clearance maintenance</p>
            <h1 id="clearance-entry-title">Clearance Maintenance</h1>
            <p>Search a vehicle, review previous records, and capture a clearance.</p>
          </div>
          <Link className="button button-secondary" href="/clearance">
            Clearance Menu
          </Link>
        </header>

        {saved ? <Message message="Clearance saved successfully." success /> : null}
        {updated ? <Message message="Clearance updated successfully." success /> : null}
        {deleted ? <Message message="Clearance deleted successfully." success /> : null}
        {errorMessage ? <Message message={errorMessage} /> : null}

        <VehicleSearch searchType={searchType} searchTerm={searchTerm} />

        {searchTerm && !vehicle ? <NotFoundState searchTerm={searchTerm} /> : null}
        {vehicle && action === "delete" && record ? (
          <DeleteConfirmation record={record} vehicle={vehicle} />
        ) : null}
        {vehicle && action !== "delete" ? (
          <ClearanceHistory records={records} vehicle={vehicle} />
        ) : null}
        {vehicle && action !== "delete" ? (
          <ClearanceForm vehicle={vehicle} merchants={merchants} record={record} />
        ) : null}
        {code && !record ? <NotFoundState searchTerm="" /> : null}
      </section>
    </main>
  );
}
