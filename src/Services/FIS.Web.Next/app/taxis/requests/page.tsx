import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { cancelTaxiRequestAction, saveTaxiRequestAction } from "@/app/taxis/actions";
import {
  dateValue,
  queryValue,
  TaxiHeader,
  TaxiNotice,
  TaxiRestricted,
  TaxiUnavailable,
  timeValue,
  valueOrDash,
} from "@/app/taxis/_components";
import {
  getTaxi,
  getTaxiByRequisition,
  getTaxiLogReferences,
  getTaxis,
  TaxiApiError,
  type TaxiRecord,
} from "@/lib/api-taxis";
import { getDepartments } from "@/lib/api-departments";
import { getSites } from "@/lib/api-sites";
import { getSession } from "@/lib/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function dateInput(value: string | null | undefined) {
  return value?.slice(0, 10) ?? "";
}

function timeInput(value: string | null | undefined) {
  if (!value) return "";
  const match = value.match(/T(\d{2}:\d{2})/);
  return match?.[1] ?? value.slice(0, 5);
}

function numberValue(value: number | null | undefined) {
  return value === null || value === undefined ? "" : String(value);
}

type LookupOption = { value: string; label: string };

type TaxiClassLookupOption = {
  classId: number;
  contractorId: number;
  contractorName: string;
  description: string;
};

type TaxiRequestLookups = {
  contractors: LookupOption[];
  classes: TaxiClassLookupOption[];
  departments: LookupOption[];
  sites: LookupOption[];
  warnings: string[];
};

function namedOption(code: number, description: string | null | undefined): LookupOption | null {
  const normalizedDescription = description?.trim();
  return normalizedDescription
    ? { value: String(code), label: `${normalizedDescription} (${code})` }
    : null;
}

function contractorOptions(
  references: Awaited<ReturnType<typeof getTaxiLogReferences>>,
) {
  return references.contractors
    .map((contractor) => ({
      value: String(contractor.contractorId),
      label: `${contractor.contractorName} (${contractor.contractorId})`,
    }))
    .toSorted((left, right) => left.label.localeCompare(right.label));
}

function classOptions(
  references: Awaited<ReturnType<typeof getTaxiLogReferences>>,
) {
  const contractorNames = new Map(
    references.contractors.map((contractor) => [contractor.contractorId, contractor.contractorName]),
  );

  return references.classes
    .filter((taxiClass) => contractorNames.has(taxiClass.contractorId))
    .map((taxiClass) => ({
      classId: taxiClass.classId,
      contractorId: taxiClass.contractorId,
      contractorName:
        contractorNames.get(taxiClass.contractorId) ?? `Contractor ${taxiClass.contractorId}`,
      description: taxiClass.description,
    }))
    .toSorted((left, right) =>
      `${left.contractorName} ${left.description}`.localeCompare(
        `${right.contractorName} ${right.description}`,
      ),
    );
}

function departmentOptions(
  departments: Awaited<ReturnType<typeof getDepartments>>,
) {
  return departments
    .filter((department) => department.deptActive)
    .map((department) => namedOption(department.departmentCode, department.description))
    .filter((option): option is LookupOption => option !== null)
    .toSorted((left, right) => left.label.localeCompare(right.label));
}

function siteOptions(sites: Awaited<ReturnType<typeof getSites>>) {
  return sites
    .filter((site) => site.siteActive)
    .map((site) => {
      const department = site.departmentNumber ?? site.departmentCode;
      const description = department
        ? `${site.description ?? "Site"} — Department ${department}`
        : site.description;
      return namedOption(site.siteCode, description);
    })
    .filter((option): option is LookupOption => option !== null)
    .toSorted((left, right) => left.label.localeCompare(right.label));
}

function lookupWarning(
  result: PromiseSettledResult<unknown>,
  options: readonly LookupOption[] | readonly TaxiClassLookupOption[],
  label: string,
) {
  if (result.status === "rejected")
    return `${label} choices are temporarily unavailable. They cannot be entered manually.`;
  if (options.length === 0)
    return `No ${label.toLowerCase()} choices are currently available. They cannot be entered manually.`;
  return null;
}

async function getTaxiRequestLookups(): Promise<TaxiRequestLookups> {
  const [referenceResult, departmentResult, siteResult] = await Promise.allSettled([
    getTaxiLogReferences(),
    getDepartments(),
    getSites(),
  ]);

  const references = referenceResult.status === "fulfilled" ? referenceResult.value : null;
  const contractors = references ? contractorOptions(references) : [];
  const classes = references ? classOptions(references) : [];
  const departments =
    departmentResult.status === "fulfilled" ? departmentOptions(departmentResult.value) : [];
  const sites = siteResult.status === "fulfilled" ? siteOptions(siteResult.value) : [];
  const warnings = [
    lookupWarning(referenceResult, contractors, "Service provider"),
    lookupWarning(referenceResult, classes, "Vehicle class"),
    lookupWarning(departmentResult, departments, "Department"),
    lookupWarning(siteResult, sites, "Site"),
  ].filter((warning): warning is string => warning !== null);

  return { contractors, classes, departments, sites, warnings };
}

function hasOption(options: readonly LookupOption[], value: string) {
  return options.some((option) => option.value === value);
}

function hasClassOption(options: readonly TaxiClassLookupOption[], value: string) {
  return options.some((option) => String(option.classId) === value);
}

function classOptionsForTaxi(
  taxi: TaxiRecord | undefined,
  classes: readonly TaxiClassLookupOption[],
) {
  if (!taxi) return classes;
  if (taxi.contractorId === null) return [];
  const providerClasses = classes.filter((taxiClass) => taxiClass.contractorId === taxi.contractorId);
  return providerClasses.length > 0 ? providerClasses : classes;
}

function currentLookupWarnings(taxi: TaxiRecord | undefined, lookups: TaxiRequestLookups) {
  if (!taxi) return lookups.warnings;

  const warnings = [...lookups.warnings];
  const contractorValue = numberValue(taxi.contractorId);
  const departmentValue = numberValue(taxi.departmentCode);
  const siteValue = numberValue(taxi.siteCode);
  const classValue = numberValue(taxi.vehicleTypeCode);
  const taxiClasses = classOptionsForTaxi(taxi, lookups.classes);

  if (lookups.contractors.length > 0 && contractorValue && !hasOption(lookups.contractors, contractorValue))
    warnings.push(
      `The current service provider is no longer in active reference data. Choose a current provider before saving.`,
    );
  if (lookups.departments.length > 0 && departmentValue && !hasOption(lookups.departments, departmentValue))
    warnings.push(
      `The current department is no longer in active reference data. Choose a current department before saving.`,
    );
  if (lookups.sites.length > 0 && siteValue && !hasOption(lookups.sites, siteValue))
    warnings.push(`The current site is no longer in active reference data. Choose a current site before saving.`);
  if (lookups.classes.length > 0 && taxiClasses.length === 0 && classValue)
    warnings.push(
      "The current provider has no active vehicle-class reference. Choose a current provider and vehicle class before saving.",
    );
  else if (taxiClasses.length > 0 && classValue && !hasClassOption(taxiClasses, classValue))
    warnings.push(
      "The current vehicle class is no longer available for this provider. Choose a current vehicle class before saving.",
    );

  return [...new Set(warnings)];
}

function LookupSelect({
  id,
  label,
  name,
  defaultValue,
  options,
  placeholder,
  required = false,
  disabled = false,
}: Readonly<{
  id: string;
  label: string;
  name: string;
  defaultValue: string;
  options: readonly LookupOption[];
  placeholder: string;
  required?: boolean;
  disabled?: boolean;
}>) {
  return (
    <div className="form-field">
      <label className="form-label" htmlFor={id}>
        {label}
        {required ? " *" : ""}
      </label>
      <select
        className="form-select"
        id={id}
        name={name}
        defaultValue={defaultValue}
        required={required}
        disabled={disabled}
      >
        <option value="">{placeholder}</option>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </div>
  );
}

function TaxiClassLookup({
  defaultValue,
  options,
  disabled = false,
}: Readonly<{
  defaultValue: string;
  options: readonly TaxiClassLookupOption[];
  disabled?: boolean;
}>) {
  const grouped = new Map<number, TaxiClassLookupOption[]>();
  for (const option of options) {
    const contractorClasses = grouped.get(option.contractorId) ?? [];
    contractorClasses.push(option);
    grouped.set(option.contractorId, contractorClasses);
  }
  const groups = [...grouped.entries()].toSorted((left, right) =>
    left[1][0].contractorName.localeCompare(right[1][0].contractorName),
  );

  return (
    <div className="form-field">
      <label className="form-label" htmlFor="taxi-class">
        Vehicle class
      </label>
      <select
        className="form-select"
        id="taxi-class"
        name="vehicleTypeCode"
        defaultValue={defaultValue}
        disabled={disabled}
      >
        <option value="">Select vehicle class...</option>
        {groups.map(([contractorId, contractorClasses]) => (
          <optgroup key={contractorId} label={contractorClasses[0].contractorName}>
            {contractorClasses.map((taxiClass) => (
              <option key={taxiClass.classId} value={String(taxiClass.classId)}>
                {taxiClass.description} ({taxiClass.classId})
              </option>
            ))}
          </optgroup>
        ))}
      </select>
    </div>
  );
}

function TaxiLookupFallbackNotice({ warnings }: Readonly<{ warnings: readonly string[] }>) {
  if (warnings.length === 0) return null;
  return (
    <aside className="notice notice-error" role="status" aria-live="polite">
      <p className="eyebrow">Reference data unavailable</p>
      <p>Some named request choices could not be loaded. Internal codes cannot be entered manually.</p>
      {warnings.map((warning) => (
        <p key={warning}>{warning}</p>
      ))}
    </aside>
  );
}

function RequestSearch({
  mode,
  query,
}: Readonly<{ mode: string; query: Record<string, string | string[] | undefined> }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <input type="hidden" name="mode" value={mode} />
      <div className="vehicle-search-row">
        <label className="form-label" htmlFor="taxi-request-search">
          Request ID or requisition number
        </label>
        <input
          className="vehicle-search"
          id="taxi-request-search"
          name="requestId"
          inputMode="numeric"
          defaultValue={queryValue(query.requestId)}
          placeholder="Request ID"
        />
        <span className="muted-copy">or use</span>
        <input
          className="vehicle-search"
          name="rekNum"
          defaultValue={queryValue(query.rekNum)}
          placeholder="Requisition number"
        />
        <button className="button button-primary" type="submit">
          Find requisition
        </button>
      </div>
    </form>
  );
}

function TaxiRequestForm({
  taxi,
  lookups,
}: Readonly<{ taxi?: TaxiRecord; lookups: TaxiRequestLookups }>) {
  const isEdit = Boolean(taxi);
  const contractorValue = numberValue(taxi?.contractorId);
  const departmentValue = numberValue(taxi?.departmentCode);
  const siteValue = numberValue(taxi?.siteCode);
  const classValue = numberValue(taxi?.vehicleTypeCode);
  const contractorIsAvailable = !isEdit || !contractorValue || hasOption(lookups.contractors, contractorValue);
  const classIsAvailable = !isEdit || !classValue || hasClassOption(lookups.classes, classValue);
  const departmentIsAvailable = !isEdit || !departmentValue || hasOption(lookups.departments, departmentValue);
  const siteIsAvailable = !isEdit || !siteValue || hasOption(lookups.sites, siteValue);
  const canSave = lookups.sites.length > 0;

  return (
    <form className="vehicle-status-maintenance-panel" action={saveTaxiRequestAction}>
      <input type="hidden" name="requestId" value={taxi?.requestId ?? ""} />
      <input type="hidden" name="returnPath" value="/taxis/requests" />
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{taxi ? "Edit requisition" : "New requisition"}</p>
          <h2>{taxi ? `Request ${taxi.rekNum}` : "Government Motor Transport"}</h2>
        </div>
        <span className="muted-copy">Fields marked required are needed to save.</span>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="taxi-rek">
            Requisition number *
          </label>
          <input
            className="form-input"
            id="taxi-rek"
            name="rekNum"
            required
            maxLength={50}
            defaultValue={taxi?.rekNum ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="taxi-official">
            Official/passenger *
          </label>
          <input
            className="form-input"
            id="taxi-official"
            name="official"
            required
            maxLength={100}
            defaultValue={taxi?.official ?? ""}
          />
        </div>
        <LookupSelect
          id="taxi-contractor"
          label="Provider / contractor"
          name="contractorId"
          defaultValue={contractorIsAvailable ? contractorValue : ""}
          options={lookups.contractors}
          placeholder="Select provider..."
          disabled={lookups.contractors.length === 0}
        />
        <TaxiClassLookup
          defaultValue={classIsAvailable ? classValue : ""}
          options={lookups.classes}
          disabled={lookups.classes.length === 0}
        />
        <div className="form-field">
          <label className="form-label" htmlFor="taxi-vmf">
            GG / vehicle code
          </label>
          <input
            className="form-input"
            id="taxi-vmf"
            name="vmfCode"
            defaultValue={taxi?.vmfCode ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="taxi-registration">
            Registration number
          </label>
          <input
            className="form-input"
            id="taxi-registration"
            name="regNum"
            defaultValue={taxi?.regNum ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="taxi-rank">
            Rank / driver detail
          </label>
          <input
            className="form-input"
            id="taxi-rank"
            name="rank"
            defaultValue={taxi?.rank ?? ""}
          />
        </div>
        <LookupSelect
          id="taxi-department"
          label="Department"
          name="departmentCode"
          defaultValue={departmentIsAvailable ? departmentValue : ""}
          options={lookups.departments}
          placeholder="Select department..."
          disabled={lookups.departments.length === 0}
        />
        <LookupSelect
          id="taxi-site"
          label="Site"
          name="siteCode"
          defaultValue={siteIsAvailable ? siteValue : ""}
          options={lookups.sites}
          placeholder="Select site..."
          required
          disabled={lookups.sites.length === 0}
        />
        <div className="form-field">
          <label className="form-label" htmlFor="taxi-date">
            Date required *
          </label>
          <input
            className="form-input"
            id="taxi-date"
            name="dateRequired"
            type="date"
            required
            defaultValue={dateInput(taxi?.dateRequired)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="taxi-time">
            Time required *
          </label>
          <input
            className="form-input"
            id="taxi-time"
            name="timeRequired"
            type="time"
            required
            defaultValue={timeInput(taxi?.timeRequired)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="taxi-flight">
            Flight
          </label>
          <input
            className="form-input"
            id="taxi-flight"
            name="flight"
            defaultValue={taxi?.flight ?? ""}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="taxi-address1">
            From / address line 1
          </label>
          <input
            className="form-input"
            id="taxi-address1"
            name="address1"
            defaultValue={taxi?.address1 ?? ""}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="taxi-address2">
            Address line 2
          </label>
          <input
            className="form-input"
            id="taxi-address2"
            name="address2"
            defaultValue={taxi?.address2 ?? ""}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="taxi-address3">
            Address line 3
          </label>
          <input
            className="form-input"
            id="taxi-address3"
            name="address3"
            defaultValue={taxi?.address3 ?? ""}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="taxi-destination1">
            Destination line 1
          </label>
          <input
            className="form-input"
            id="taxi-destination1"
            name="destination1"
            defaultValue={taxi?.destination1 ?? ""}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="taxi-destination2">
            Destination line 2
          </label>
          <input
            className="form-input"
            id="taxi-destination2"
            name="destination2"
            defaultValue={taxi?.destination2 ?? ""}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="taxi-instructions">
            Instructions
          </label>
          <textarea
            className="form-textarea"
            id="taxi-instructions"
            name="instructions"
            defaultValue={taxi?.instructions ?? ""}
          />
        </div>
        <label className="vehicle-checkbox-label">
          <input
            name="driverAvailable"
            type="checkbox"
            defaultChecked={taxi?.driverAvailable ?? false}
          />{" "}
          Driver available
        </label>
        <label className="vehicle-checkbox-label">
          <input name="jiaPickup" type="checkbox" defaultChecked={taxi?.jiaPickup ?? false} /> JIA
          pick-up
        </label>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit" disabled={!canSave}>
          {taxi ? "Save changes" : "Enter requisition"}
        </button>
        <Link className="button button-secondary" href="/taxis">
          Menu
        </Link>
      </div>
    </form>
  );
}

function RequestSummary({ taxi }: Readonly<{ taxi: TaxiRecord }>) {
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="taxi-request-summary-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Requisition</p>
          <h2 id="taxi-request-summary-title">{taxi.rekNum}</h2>
        </div>
        <span className="muted-copy">{taxi.cancelled ? "Cancelled" : "Active"}</span>
      </div>
      <dl className="details-grid">
        <div>
          <dt>Official</dt>
          <dd>{valueOrDash(taxi.official)}</dd>
        </div>
        <div>
          <dt>Vehicle</dt>
          <dd>{valueOrDash(taxi.vmfCode)}</dd>
        </div>
        <div>
          <dt>Department</dt>
          <dd>{valueOrDash(taxi.departmentName ?? taxi.departmentCode)}</dd>
        </div>
        <div>
          <dt>Site</dt>
          <dd>{valueOrDash(taxi.siteName ?? taxi.siteCode)}</dd>
        </div>
        <div>
          <dt>Date required</dt>
          <dd>{dateValue(taxi.dateRequired)}</dd>
        </div>
        <div>
          <dt>Time required</dt>
          <dd>{timeValue(taxi.timeRequired)}</dd>
        </div>
        <div>
          <dt>From</dt>
          <dd>{valueOrDash(taxi.address1)}</dd>
        </div>
        <div>
          <dt>Destination</dt>
          <dd>{valueOrDash(taxi.destination1 ?? taxi.address2)}</dd>
        </div>
      </dl>
    </section>
  );
}

export default async function TaxiRequestsPage({
  searchParams,
  mode: forcedMode,
}: Readonly<{ searchParams: SearchParams; mode?: string }>) {
  await connection();
  const session = await getSession();
  const routePath = "/taxis/requests";
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (
    !session.roles.some(
      (role) =>
        role.localeCompare("Private Hire Vehicles", undefined, { sensitivity: "accent" }) === 0,
    )
  )
    return (
      <main className="page-shell vehicle-page-shell">
        <TaxiRestricted subject="Taxi requisitions" />
      </main>
    );

  const query = await searchParams;
  const mode = forcedMode ?? (queryValue(query.mode) || "add");
  const requestId = Number(queryValue(query.requestId));
  const rekNum = queryValue(query.rekNum);
  try {
    if (mode === "pending" || mode === "pending-jia") {
      const taxis = await getTaxis();
      const filtered = taxis.filter(
        (taxi) => !taxi.cancelled && (mode !== "pending-jia" || taxi.jiaPickup),
      );
      return (
        <main className="page-shell vehicle-page-shell">
          <section className="vehicle-card">
            <TaxiHeader
              title={mode === "pending-jia" ? "Pending JIA Pick-ups" : "Pending Taxi Requests"}
              description="Review active taxi requisitions that still require attention."
            />
            <TaxiNotice query={query} />
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <caption className="sr-only">Pending taxi requests</caption>
                <thead>
                  <tr>
                    <th scope="col">Requisition</th>
                    <th scope="col">Official</th>
                    <th scope="col">Date</th>
                    <th scope="col">Vehicle</th>
                    <th scope="col">Department</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.length === 0 ? (
                    <tr>
                      <td colSpan={5}>No pending records found.</td>
                    </tr>
                  ) : (
                    filtered.slice(0, 500).map((taxi) => (
                      <tr key={taxi.requestId}>
                        <td>
                          <Link href={`/taxis/requests?mode=edit&requestId=${taxi.requestId}`}>
                            {taxi.rekNum}
                          </Link>
                        </td>
                        <td>{valueOrDash(taxi.official)}</td>
                        <td>{dateValue(taxi.dateRequired)}</td>
                        <td>{valueOrDash(taxi.vmfCode)}</td>
                        <td>{valueOrDash(taxi.departmentName ?? taxi.departmentCode)}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </section>
        </main>
      );
    }

    let taxi: TaxiRecord | undefined;
    if (Number.isInteger(requestId) && requestId > 0) taxi = await getTaxi(requestId);
    else if (rekNum) taxi = await getTaxiByRequisition(rekNum);

    if (mode === "cancel")
      return (
        <main className="page-shell vehicle-page-shell">
          <section className="vehicle-card">
            <TaxiHeader
              title="Cancel Taxi Requisition"
              description="Cancel an existing taxi requisition while preserving its legacy record."
            />
            <TaxiNotice query={query} />
            <RequestSearch mode="cancel" query={query} />
            {taxi ? (
              <>
                <RequestSummary taxi={taxi} />
                <form className="vehicle-status-maintenance-panel" action={cancelTaxiRequestAction}>
                  <input type="hidden" name="requestId" value={taxi.requestId} />
                  <input type="hidden" name="returnPath" value={routePath + "?mode=cancel"} />
                  <div className="form-field">
                    <label className="form-label" htmlFor="cancel-reason">
                      Cancellation reason
                    </label>
                    <input
                      className="form-input"
                      id="cancel-reason"
                      name="cancelReason"
                      defaultValue={taxi.cancelled ?? ""}
                    />
                  </div>
                  <div className="button-row">
                    <button
                      className="button button-primary"
                      type="submit"
                      disabled={Boolean(taxi.cancelled)}
                    >
                      Cancel requisition
                    </button>
                    <Link className="button button-secondary" href="/taxis">
                      Menu
                    </Link>
                  </div>
                </form>
              </>
            ) : null}
          </section>
        </main>
      );
    if (mode === "reprint")
      return (
        <main className="page-shell vehicle-page-shell">
          <section className="vehicle-card">
            <TaxiHeader
              title="Re-Print A Requisition"
              description="Find a requisition and review its printable details."
            />
            <TaxiNotice query={query} />
            <RequestSearch mode="reprint" query={query} />
            {taxi ? <RequestSummary taxi={taxi} /> : null}
          </section>
        </main>
      );
    if (mode === "edit" && !taxi)
      return (
        <main className="page-shell vehicle-page-shell">
          <section className="vehicle-card">
            <TaxiHeader
              title="Edit Taxi Requisition"
              description="Enter a request ID or requisition number to continue."
            />
            <TaxiNotice query={query} />
            <RequestSearch mode="edit" query={query} />
          </section>
        </main>
      );
    const lookups = await getTaxiRequestLookups();
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card">
          <TaxiHeader
            title={mode === "edit" ? "Edit Taxi Requisition" : "Enter Taxi Requisition"}
            description="Capture the taxi requisition using the existing FIS business fields."
          />
          <TaxiNotice query={query} />
          {mode === "edit" ? <RequestSearch mode="edit" query={query} /> : null}
          <TaxiLookupFallbackNotice
            warnings={currentLookupWarnings(mode === "edit" ? taxi : undefined, lookups)}
          />
          <TaxiRequestForm taxi={mode === "edit" ? taxi : undefined} lookups={lookups} />
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof TaxiApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <TaxiUnavailable path={routePath} subject="Taxi requisitions" />
      </main>
    );
  }
}
