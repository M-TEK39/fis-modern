import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { saveFineAction } from "@/app/(fleet-operations)/fines/actions";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SearchTypeFieldset from "@/components/ui/search-type-fieldset";
import {
  FineApiError,
  getFine,
  getFineSites,
  getFineVehicle,
  getTrafficDepts,
  searchFineVehicles,
  type FineRecord,
  type FineSearchType,
  type FineSite,
  type FineVehicleOption,
  type TrafficDeptRecord,
} from "@/lib/api/fleet-operations/api-fines";
import { getSession } from "@/lib/auth/session";
import { hasFinesAccess } from "@/app/(fleet-operations)/fines/access";

export type FineDetailPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

const DOCUMENT_TYPES = [
  ["Notice Offence", "Notice of Traffic Offence"],
  ["Summons to Issued", "Notice of Summons to be Issued"],
  ["Summons Criminal", "Summons in Criminal Case"],
  ["Warrent Arrest", "Warrant for Arrest"],
  ["Warrant Arrest", "Warrant for Arrest"],
] as const;

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getSearchType(value: string | undefined): FineSearchType {
  return value === "GG" || value === "Radiogg" ? "GG" : "GP";
}

function getPositiveQueryInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasReportsRole(roles: readonly string[]) {
  return hasFinesAccess(roles);
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function dateInputValue(value: string | null | undefined) {
  return value?.slice(0, 10) ?? "";
}

function buildSearchHref(searchType: FineSearchType, searchQuery: string) {
  const params = new URLSearchParams({ searchType });
  if (searchQuery) {
    params.set("searchQuery", searchQuery);
  }
  return `/fines/maintenance/detail?${params.toString()}`;
}

function VehicleLookup({
  searchType,
  searchQuery,
}: Readonly<{ searchType: FineSearchType; searchQuery: string }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <input name="route" type="hidden" value="add" />
      <SearchTypeFieldset selectedType={searchType} legend="Find vehicle by" />
      <div className="vehicle-search-row">
        <label className="sr-only" htmlFor="fine-detail-vehicle-search">
          {searchType === "GG" ? "GG number" : "GP number"}
        </label>
        <input
          className="vehicle-search"
          id="fine-detail-vehicle-search"
          maxLength={8}
          name="searchQuery"
          placeholder={
            searchType === "GG" ? "Enter GG number" : "Enter current or historical GP number"
          }
          defaultValue={searchQuery}
        />
      </div>
      <div className="button-row">
        <button className="button button-secondary" type="submit">
          Find
        </button>
        <Link className="button button-secondary" href="/fines/maintenance">
          Back to Fines
        </Link>
      </div>
    </form>
  );
}

function VehicleSelection({
  fine,
  vehicle,
  vehicles,
}: Readonly<{
  fine: FineRecord | null;
  vehicle: FineVehicleOption | null;
  vehicles: FineVehicleOption[];
}>) {
  if (fine) {
    return (
      <div className="form-field form-group-full">
        <label className="form-label" htmlFor="fine-vehicle-readonly">
          Vehicle
        </label>
        <input
          className="form-input"
          id="fine-vehicle-readonly"
          readOnly
          value={`${valueOrDash(vehicle?.fleetNumber)} / ${valueOrDash(vehicle?.registrationNumber)} (${fine.vmfCode ?? "-"})`}
        />
        <input name="vmfCode" type="hidden" value={fine.vmfCode ?? ""} />
      </div>
    );
  }

  return (
    <div className="form-field form-group-full">
      <label className="form-label" htmlFor="fine-vehicle">
        Vehicle
      </label>
      <select
        className="form-select"
        id="fine-vehicle"
        name="vmfCode"
        defaultValue={vehicle?.vmfCode ?? ""}
        required
      >
        <option value="">Select vehicle...</option>
        {vehicles.map((option) => (
          <option key={option.vmfCode} value={option.vmfCode}>
            {valueOrDash(option.fleetNumber)} / {valueOrDash(option.registrationNumber)} (
            {option.vmfCode})
            {option.isHistoricalMatch && option.matchedRegistration
              ? ` — historical GP ${option.matchedRegistration}`
              : ""}
          </option>
        ))}
      </select>
      {vehicles.length === 0 ? (
        <p className="form-hint">
          Search for the GG or current/historical GP number above before saving.
        </p>
      ) : null}
      {vehicle?.isHistoricalMatch && vehicle.matchedRegistration ? (
        <p className="form-hint">
          Historical GP {vehicle.matchedRegistration} resolves to the current vehicle shown above.
        </p>
      ) : null}
    </div>
  );
}

function FineForm({
  fine,
  vehicle,
  vehicles,
  sites,
  trafficDepts,
  searchType,
  searchQuery,
}: Readonly<{
  fine: FineRecord | null;
  vehicle: FineVehicleOption | null;
  vehicles: FineVehicleOption[];
  sites: FineSite[];
  trafficDepts: TrafficDeptRecord[];
  searchType: FineSearchType;
  searchQuery: string;
}>) {
  const isEdit = fine !== null;
  const selectedDocumentType = fine?.documentType ?? "Notice Offence";

  return (
    <form className="vehicle-status-maintenance-panel" action={saveFineAction}>
      {fine ? <input name="fineCode" type="hidden" value={fine.fineCode} /> : null}
      <input name="returnSearchType" type="hidden" value={searchType} />
      <input name="returnSearchQuery" type="hidden" value={searchQuery} />
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{isEdit ? "Existing record" : "New record"}</p>
          <h2>{isEdit ? `Edit Fine #${fine.fineCode}` : "Capture Fine"}</h2>
        </div>
      </div>
      <div className="form-grid">
        <VehicleSelection fine={fine} vehicle={vehicle} vehicles={vehicles} />

        <div className="form-field">
          <label className="form-label" htmlFor="fine-offence-date">
            Date of Offence
          </label>
          <input
            className="form-input"
            id="fine-offence-date"
            name="offenceDate"
            type="date"
            defaultValue={dateInputValue(fine?.offenceDate)}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="fine-reference">
            Reference Number
          </label>
          <input
            className="form-input"
            id="fine-reference"
            maxLength={20}
            name="offenceReference"
            defaultValue={fine?.offenceReference ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="fine-issuer">
            Issued by
          </label>
          <input
            className="form-input"
            id="fine-issuer"
            maxLength={20}
            name="offenceIssuer"
            defaultValue={fine?.offenceIssuer ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="fine-traffic-dept">
            Traffic Dept
          </label>
          <select
            className="form-select"
            id="fine-traffic-dept"
            name="trafficDeptCode"
            defaultValue={fine?.trafficDeptCode ?? ""}
          >
            <option value="">Select Traffic Dept</option>
            {trafficDepts.map((dept) => (
              <option key={dept.trafficDeptCode} value={dept.trafficDeptCode}>
                {valueOrDash(dept.name)} ({dept.trafficDeptCode})
              </option>
            ))}
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="fine-amount">
            Amount of Fine
          </label>
          <input
            className="form-input"
            id="fine-amount"
            maxLength={8}
            min="0"
            name="fineAmount"
            step="0.01"
            type="number"
            defaultValue={fine?.fineAmount ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="fine-pay-due-date">
            Due Date of Payment
          </label>
          <input
            className="form-input"
            id="fine-pay-due-date"
            name="payDueDate"
            type="date"
            defaultValue={dateInputValue(fine?.payDueDate)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="fine-appear-date">
            Due Date to Appear in Court
          </label>
          <input
            className="form-input"
            id="fine-appear-date"
            name="appearDate"
            type="date"
            defaultValue={dateInputValue(fine?.appearDate)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="fine-document-type">
            Document Type
          </label>
          <select
            className="form-select"
            id="fine-document-type"
            name="documentType"
            defaultValue={selectedDocumentType}
          >
            {DOCUMENT_TYPES.map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
            {!DOCUMENT_TYPES.some(([value]) => value === selectedDocumentType) ? (
              <option value={selectedDocumentType}>{selectedDocumentType}</option>
            ) : null}
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="fine-receive-date">
            Date Received at GMT
          </label>
          <input
            className="form-input"
            id="fine-receive-date"
            name="receiveGgDate"
            type="date"
            defaultValue={dateInputValue(fine?.receiveGgDate)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="fine-issuer-notify-date">
            Date of Notification to Issuer
          </label>
          <input
            className="form-input"
            id="fine-issuer-notify-date"
            name="issuerNotifyDate"
            type="date"
            defaultValue={dateInputValue(fine?.issuerNotifyDate)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="fine-dept-notify-date">
            Date of Notification to Dept
          </label>
          <input
            className="form-input"
            id="fine-dept-notify-date"
            name="notifyDeptDate"
            type="date"
            defaultValue={dateInputValue(fine?.notifyDeptDate)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="fine-site">
            Dept Code / Site
          </label>
          <select
            className="form-select"
            id="fine-site"
            name="siteCode"
            defaultValue={fine?.siteCode ?? ""}
          >
            <option value="">Select Dept/Site</option>
            {sites.map((site) => (
              <option key={site.siteCode} value={site.siteCode}>
                {valueOrDash(site.departmentNumber)} / {valueOrDash(site.description)} (
                {site.siteCode})
              </option>
            ))}
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="fine-dept-person-name">
            Name Responsible person at Dept
          </label>
          <input
            className="form-input"
            id="fine-dept-person-name"
            maxLength={25}
            name="deptPersonName"
            defaultValue={fine?.deptPersonName ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="fine-dept-person-id">
            ID Responsible person at Dept
          </label>
          <input
            className="form-input"
            id="fine-dept-person-id"
            maxLength={13}
            name="deptPersonId"
            defaultValue={fine?.deptPersonId ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="fine-paid-date">
            Date Fine Paid
          </label>
          <input
            className="form-input"
            id="fine-paid-date"
            name="finePayDate"
            type="date"
            defaultValue={dateInputValue(fine?.finePayDate)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="fine-withdrawn-date">
            Date Withdrawn
          </label>
          <input
            className="form-input"
            id="fine-withdrawn-date"
            name="withdrawDate"
            type="date"
            defaultValue={dateInputValue(fine?.withdrawDate)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="fine-offence-name">
            Name of Offender
          </label>
          <input
            className="form-input"
            id="fine-offence-name"
            maxLength={20}
            name="offenceName"
            defaultValue={fine?.offenceName ?? ""}
          />
        </div>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          {isEdit ? "Update" : "Submit"}
        </button>
        <Link className="button button-secondary" href="/fines/maintenance">
          Menu
        </Link>
      </div>
    </form>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>Fine details could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href="/fines/maintenance/detail">
        Try again
      </Link>
    </section>
  );
}

const FineDetailContent = renderFineDetailContent;

async function renderFineDetailContent({ searchParams, routePath }: FineDetailPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") {
    redirect("/login");
  }
  if (session.status === "expired") {
    return <SessionRecovery returnPath={routePath ?? "/fines/maintenance/detail"} />;
  }
  if (session.status === "unavailable") {
    return <ApiUnavailable />;
  }
  if (!hasReportsRole(session.roles)) {
    return (
      <section className="vehicle-status-card" role="alert">
        <div className="status-icon status-icon-error" aria-hidden="true">
          !
        </div>
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to maintain Fines.</h2>
      </section>
    );
  }

  const query = await searchParams;
  const fineCode = getPositiveQueryInt(getQueryValue(query.fineId) ?? getQueryValue(query.FCode));
  const vmfCode = getPositiveQueryInt(getQueryValue(query.vmfCode) ?? getQueryValue(query.Code));
  const searchType = getSearchType(getQueryValue(query.searchType) ?? getQueryValue(query.Radio1));
  const searchQuery = (getQueryValue(query.searchQuery) ?? getQueryValue(query.txtGGNum) ?? "")
    .trim()
    .slice(0, 8);

  try {
    const [fine, sites, trafficDepts] = await Promise.all([
      fineCode ? getFine(fineCode) : Promise.resolve(null),
      getFineSites(),
      getTrafficDepts(),
    ]);

    let vehicles: FineVehicleOption[] = [];
    if (fine?.vmfCode) {
      vehicles = [await getFineVehicle(fine.vmfCode)];
    } else if (searchQuery) {
      vehicles = await searchFineVehicles(searchType, searchQuery);
    } else if (vmfCode) {
      vehicles = [await getFineVehicle(vmfCode)];
    }

    const selectedVehicle = fine?.vmfCode
      ? (vehicles.find((vehicle) => vehicle.vmfCode === fine.vmfCode) ?? null)
      : vmfCode
        ? (vehicles.find((vehicle) => vehicle.vmfCode === vmfCode) ?? null)
        : vehicles.length === 1
          ? vehicles[0]
          : null;

    return (
      <>
        {!fine ? <VehicleLookup searchType={searchType} searchQuery={searchQuery} /> : null}
        {!fine && searchQuery && vehicles.length === 0 ? (
          <div className="vehicle-empty-state">
            <p className="eyebrow">Vehicle not found</p>
            <h2>No vehicle matched “{searchQuery}”.</h2>
            <p className="muted-copy">Check the lookup type and try again.</p>
          </div>
        ) : null}
        <FineForm
          fine={fine}
          vehicle={selectedVehicle}
          vehicles={vehicles}
          sites={sites}
          trafficDepts={trafficDepts}
          searchType={searchType}
          searchQuery={searchQuery}
        />
      </>
    );
  } catch (error) {
    if (error instanceof FineApiError && error.reason === "unauthorized") {
      return <SessionRecovery returnPath={routePath ?? "/fines/maintenance/detail"} />;
    }

    console.error(
      "FIS fine detail request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return <ApiUnavailable />;
  }
}

export default function FineDetailPage(props: FineDetailPageProps) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="fine-detail-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Fines maintenance</p>
            <h1 id="fine-detail-title">Fine Maintenance Detail</h1>
            <p>
              Add or update a fine against the correct vehicle, including historical GP matches.
            </p>
          </div>
          <Link className="button button-secondary" href="/fines">
            Fines Menu
          </Link>
        </header>
        <StreamedRoute>
          <FineDetailContent {...props} />
        </StreamedRoute>
      </section>
    </main>
  );
}
