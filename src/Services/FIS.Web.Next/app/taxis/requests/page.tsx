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
  getTaxis,
  TaxiApiError,
  type TaxiRecord,
} from "@/lib/api-taxis";
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

function TaxiRequestForm({ taxi }: Readonly<{ taxi?: TaxiRecord }>) {
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
        <div className="form-field">
          <label className="form-label" htmlFor="taxi-contractor">
            Service provider ID
          </label>
          <input
            className="form-input"
            id="taxi-contractor"
            name="contractorId"
            type="number"
            min="1"
            defaultValue={numberValue(taxi?.contractorId)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="taxi-class">
            Vehicle class
          </label>
          <input
            className="form-input"
            id="taxi-class"
            name="vehicleTypeCode"
            type="number"
            min="0"
            defaultValue={numberValue(taxi?.vehicleTypeCode)}
          />
        </div>
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
        <div className="form-field">
          <label className="form-label" htmlFor="taxi-department">
            Department code
          </label>
          <input
            className="form-input"
            id="taxi-department"
            name="departmentCode"
            type="number"
            min="1"
            defaultValue={numberValue(taxi?.departmentCode)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="taxi-site">
            Site code *
          </label>
          <input
            className="form-input"
            id="taxi-site"
            name="siteCode"
            type="number"
            min="1"
            required
            defaultValue={taxi?.siteCode || ""}
          />
        </div>
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
        <button className="button button-primary" type="submit">
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
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card">
          <TaxiHeader
            title={mode === "edit" ? "Edit Taxi Requisition" : "Enter Taxi Requisition"}
            description="Capture the taxi requisition using the existing FIS business fields."
          />
          <TaxiNotice query={query} />
          {mode === "edit" ? <RequestSearch mode="edit" query={query} /> : null}
          <TaxiRequestForm taxi={mode === "edit" ? taxi : undefined} />
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
