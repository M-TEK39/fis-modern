import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { deleteTowingAction, saveTowingAction } from "@/app/towing/actions";
import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";
import {
  getTowing,
  getTowingSites,
  getTowTrucks,
  TowingApiError,
  type TowingRecord,
  type TowingSearchType,
  type TowingSite,
  type TowTruckRecord,
} from "@/lib/api-towing";

const TOWING_ROLE = "Towing";

export type TowingDetailPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getPositiveInteger(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasTowingRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(TOWING_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function dateInputValue(value: string | null | undefined) {
  return value?.slice(0, 10) ?? "";
}

function timeInputValue(value: string | null | undefined) {
  if (!value) return "";
  const match = value.match(/T(\d{2}:\d{2})/);
  return match?.[1] ?? value.slice(0, 5);
}

function TowingForm({
  record,
  vmfCode,
  sites,
  towTrucks,
  routePath,
}: Readonly<{
  record: TowingRecord | null;
  vmfCode: number | null;
  sites: TowingSite[];
  towTrucks: TowTruckRecord[];
  routePath: string;
}>) {
  return (
    <form action={saveTowingAction} className="vehicle-status-maintenance-panel">
      {record ? <input name="towingCode" type="hidden" value={record.towingCode} /> : null}
      <input name="returnPath" type="hidden" value={routePath} />
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{record ? "Existing record" : "New record"}</p>
          <h2>{record ? `Edit Towing Request #${record.towingCode}` : "Capture a New Request"}</h2>
          <p>
            All legacy request fields remain available so this form can write to either supported
            database shape.
          </p>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="towing-vmf">
            Vehicle VMF code
          </label>
          <input
            className="form-input"
            id="towing-vmf"
            name="vmfCode"
            type="number"
            min="1"
            defaultValue={record?.vmfCode ?? vmfCode ?? ""}
            readOnly={Boolean(record || vmfCode)}
            required
          />
          <p className="form-hint">The vehicle was resolved by GG or GP on the previous screen.</p>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="towing-date">
            Request Date
          </label>
          <input
            className="form-input"
            id="towing-date"
            name="requestDate"
            type="date"
            defaultValue={dateInputValue(record?.requestDate)}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="towing-time">
            Request Time
          </label>
          <input
            className="form-input"
            id="towing-time"
            name="requestTime"
            type="time"
            defaultValue={timeInputValue(record?.requestTime)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="towing-reference">
            Call Ref Num
          </label>
          <input
            className="form-input"
            id="towing-reference"
            name="callReference"
            type="number"
            min="0"
            defaultValue={record?.callReference ?? ""}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="towing-location">
            Location of Vehicle
          </label>
          <input
            className="form-input"
            id="towing-location"
            name="locationStart"
            maxLength={50}
            defaultValue={record?.locationStart ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="towing-problem">
            Vehicle Problem
          </label>
          <input
            className="form-input"
            id="towing-problem"
            name="vehicleProblem"
            maxLength={30}
            defaultValue={record?.vehicleProblem ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="towing-company">
            Company
          </label>
          <select
            className="form-select"
            id="towing-company"
            name="towTruckCode"
            defaultValue={record?.towTruckCode ?? ""}
          >
            <option value="">Select company</option>
            {towTrucks.map((truck) => (
              <option key={truck.towCode} value={truck.towCode}>
                {valueOrDash(truck.name)}
                {truck.telephone ? ` — ${truck.telephone}` : ""}
              </option>
            ))}
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="towing-keys">
            Keys
          </label>
          <input
            className="form-input"
            id="towing-keys"
            name="keys"
            maxLength={200}
            defaultValue={record?.keys ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="towing-site">
            Site
          </label>
          <select
            className="form-select"
            id="towing-site"
            name="siteCode"
            defaultValue={record?.siteCode ?? ""}
          >
            <option value="">Select site</option>
            {sites.map((site) => (
              <option key={site.siteCode} value={site.siteCode}>
                {valueOrDash(site.departmentNumber)} / {valueOrDash(site.description)} (
                {site.siteCode})
              </option>
            ))}
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="towing-contact-name">
            Contact Name
          </label>
          <input
            className="form-input"
            id="towing-contact-name"
            name="contactPersonName"
            maxLength={30}
            defaultValue={record?.contactPersonName ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="towing-contact-tel">
            Contact Cell / Tel
          </label>
          <input
            className="form-input"
            id="towing-contact-tel"
            name="contactPersonTel"
            maxLength={30}
            defaultValue={record?.contactPersonTel ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="towing-contact-cell">
            Contact Cell
          </label>
          <input
            className="form-input"
            id="towing-contact-cell"
            name="contactPersonCell"
            maxLength={10}
            defaultValue={record?.contactPersonCell ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="towing-person">
            Person Name, with Vehicle
          </label>
          <input
            className="form-input"
            id="towing-person"
            name="personAtVehicleName"
            maxLength={30}
            defaultValue={record?.personAtVehicleName ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="towing-person-cell">
            Person Cell
          </label>
          <input
            className="form-input"
            id="towing-person-cell"
            name="personAtVehicleCell"
            maxLength={10}
            defaultValue={record?.personAtVehicleCell ?? ""}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="towing-remarks">
            Remarks
          </label>
          <input
            className="form-input"
            id="towing-remarks"
            name="remarks"
            maxLength={50}
            defaultValue={record?.remarks ?? ""}
          />
        </div>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          {record ? "Update" : "Submit"}
        </button>
        <Link className="button button-secondary" href="/towing/request">
          Cancel
        </Link>
      </div>
    </form>
  );
}

function DeleteForm({ record, routePath }: Readonly<{ record: TowingRecord; routePath: string }>) {
  return (
    <form action={deleteTowingAction} className="vehicle-status-maintenance-panel">
      <input name="towingCode" type="hidden" value={record.towingCode} />
      <input name="returnPath" type="hidden" value={routePath} />
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Legacy delete workflow</p>
          <h2>Delete this request?</h2>
          <p>
            Deletion uses the existing Towing record operation. On expanded databases it is
            soft-deleted; on the original schema it follows the legacy delete behavior.
          </p>
        </div>
      </div>
      <div className="button-row">
        <button className="button button-danger" type="submit">
          DELETE
        </button>
        <Link className="button button-secondary" href="/towing/request">
          Do NOT Delete
        </Link>
      </div>
    </form>
  );
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Towing request details could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href={routePath}>
        Try again
      </Link>
    </section>
  );
}

export default async function TowingDetailPage({
  searchParams,
  routePath = "/towing/request/detail",
}: TowingDetailPageProps) {
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
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  if (!hasTowingRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to maintain towing requests.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const towingCode = getPositiveInteger(
    getQueryValue(query.towingId) ?? getQueryValue(query.Tcode),
  );
  const vmfCode = getPositiveInteger(getQueryValue(query.vmfCode));
  const searchType = (
    getQueryValue(query.searchType) === "GP" ? "GP" : "GG"
  ) satisfies TowingSearchType;
  const searchQuery = (getQueryValue(query.searchQuery) ?? getQueryValue(query.xggnum) ?? "")
    .trim()
    .slice(0, 8);
  const notice =
    getQueryValue(query.error) ??
    (getQueryValue(query.saved) === "1"
      ? "Towing request captured successfully."
      : getQueryValue(query.updated) === "1"
        ? "Towing request updated successfully."
        : "");
  const isError = Boolean(getQueryValue(query.error));

  try {
    const [record, sites, towTrucks] = await Promise.all([
      towingCode ? getTowing(towingCode) : Promise.resolve(null),
      getTowingSites(),
      getTowTrucks(),
    ]);
    const resolvedVmfCode = record?.vmfCode ?? vmfCode;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="towing-detail-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Road Side Assistance</p>
              <h1 id="towing-detail-title">Road Side Assistance Request</h1>
              <p>
                {record
                  ? `Vehicle VMF code ${record.vmfCode}; edit the complete legacy request.`
                  : `Capture a new request for ${searchQuery || `vehicle ${resolvedVmfCode ?? "the selected vehicle"}`}.`}
              </p>
            </div>
            <Link className="button button-secondary" href="/towing/request">
              Request Search
            </Link>
          </header>
          {notice ? (
            <div
              className={isError ? "notice notice-error" : "notice notice-success"}
              role={isError ? "alert" : "status"}
            >
              {notice}
            </div>
          ) : null}
          <TowingForm
            record={record}
            vmfCode={resolvedVmfCode}
            sites={sites}
            towTrucks={towTrucks}
            routePath={routePath}
          />
          {record ? <DeleteForm record={record} routePath="/towing/request" /> : null}
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof TowingApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery
            returnPath={`${routePath}?${new URLSearchParams({ ...(towingCode ? { towingId: String(towingCode) } : {}), ...(vmfCode ? { vmfCode: String(vmfCode) } : {}), searchType, searchQuery }).toString()}`}
          />
        </main>
      );
    if (error instanceof TowingApiError && error.reason === "not-found")
      return (
        <main className="page-shell vehicle-page-shell">
          <section className="vehicle-status-card" role="alert">
            <p className="eyebrow">Record not found</p>
            <h2>The requested towing record was not found.</h2>
            <Link className="button button-secondary" href="/towing/request">
              Back to Towing
            </Link>
          </section>
        </main>
      );
    console.error(
      "FIS towing detail request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  }
}
