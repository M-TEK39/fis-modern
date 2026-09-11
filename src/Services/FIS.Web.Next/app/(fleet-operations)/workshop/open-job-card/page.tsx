import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { reopenWorkshopAction } from "@/app/(fleet-operations)/workshop/actions";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  getWorkshopVehicles,
  getWorkshops,
  WorkshopApiError,
} from "@/lib/api/fleet-operations/api-workshop";
import { getSession } from "@/lib/auth/session";

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

async function OpenWorkshopJobCardPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/workshop/open-job-card" />
      </main>
    );
  if (
    !session.roles.some(
      (role) => role.localeCompare("Workshop", undefined, { sensitivity: "accent" }) === 0,
    )
  )
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>Access restricted.</h2>
        </section>
      </main>
    );
  const query = await searchParams;
  const selectedCode = Number(queryValue(query.id));
  const reopened = queryValue(query.reopened) === "1";
  const errorMessage = queryValue(query.error);
  try {
    const [allWorkshops, vehicles] = await Promise.all([getWorkshops(), getWorkshopVehicles()]);
    const vehicleByCode = new Map(vehicles.map((vehicle) => [vehicle.vmfCode, vehicle]));
    const closed = allWorkshops.filter(
      (entry) => entry.completeDate !== null || entry.completeTime !== null,
    );
    const selected =
      Number.isInteger(selectedCode) && selectedCode > 0
        ? closed.find((entry) => entry.wwCode === selectedCode)
        : null;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="open-job-card-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Workshop maintenance</p>
              <h1 id="open-job-card-title">OPEN a CLOSED Job Card</h1>
              <p>Re-open a closed workshop job card by clearing its complete date and time.</p>
            </div>
            <Link className="button button-secondary" href="/workshop">
              Back
            </Link>
          </header>
          {reopened ? (
            <div className="notice notice-success" role="status">
              Workshop job card reopened successfully.
            </div>
          ) : null}
          {errorMessage ? (
            <div className="notice notice-error" role="alert">
              {errorMessage}
            </div>
          ) : null}
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="closed-entries-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {closed.length} closed record{closed.length === 1 ? "" : "s"}
                </p>
                <h2 id="closed-entries-title">Closed Workshop Entries</h2>
              </div>
            </div>
            {closed.length === 0 ? (
              <p className="muted-copy">No closed workshop entries found.</p>
            ) : (
              <div className="vehicle-table-wrapper">
                <table className="vehicle-table">
                  <caption className="sr-only">Closed workshop entries</caption>
                  <thead>
                    <tr>
                      <th scope="col">Entry</th>
                      <th scope="col">Vehicle</th>
                      <th scope="col">Completed</th>
                      <th scope="col">Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {closed.map((entry) => {
                      const vehicle =
                        entry.vmfCode === null ? undefined : vehicleByCode.get(entry.vmfCode);
                      return (
                        <tr key={entry.wwCode}>
                          <td>{entry.wwCode}</td>
                          <td>
                            {vehicle?.fleetNumber ||
                              vehicle?.registrationNumber ||
                              `VMF ${entry.vmfCode ?? "-"}`}
                          </td>
                          <td>{formatDate(entry.completeDate)}</td>
                          <td>
                            <Link
                              className="button button-secondary button-small"
                              href={`/workshop/open-job-card?id=${entry.wwCode}`}
                            >
                              Select
                            </Link>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>
          {selected ? (
            <section
              className="vehicle-status-maintenance-panel"
              aria-labelledby="reopen-selected-title"
            >
              <p className="eyebrow">Selected entry #{selected.wwCode}</p>
              <h2 id="reopen-selected-title">Confirm Reopen</h2>
              <p>
                Vehicle {selected.vmfCode ?? "-"} · completed {formatDate(selected.completeDate)}.
              </p>
              <form action={reopenWorkshopAction}>
                <input name="returnPath" type="hidden" value="/workshop/open-job-card" />
                <input name="wwCode" type="hidden" value={selected.wwCode} />
                <div className="form-field">
                  <label className="form-label" htmlFor="authorizer-password">
                    Authorizer Password
                  </label>
                  <input
                    className="form-input"
                    id="authorizer-password"
                    name="password"
                    type="password"
                    autoComplete="current-password"
                    required
                  />
                </div>
                <div className="button-row">
                  <button className="button button-primary" type="submit">
                    Reopen job card
                  </button>
                  <Link className="button button-secondary" href="/workshop/open-job-card">
                    Cancel
                  </Link>
                </div>
              </form>
            </section>
          ) : (
            <p className="muted-copy">Select a closed workshop entry to continue.</p>
          )}
        </section>
      </main>
    );
  } catch (error) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>
            {error instanceof WorkshopApiError && error.reason === "unavailable"
              ? "The workshop service is temporarily unavailable."
              : "Closed workshop entries could not be loaded."}
          </h2>
          <Link className="button button-secondary" href="/workshop">
            Back
          </Link>
        </section>
      </main>
    );
  }
}

export default function OpenWorkshopJobCardPage(
  props: Parameters<typeof OpenWorkshopJobCardPageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <OpenWorkshopJobCardPageContent {...props} />
    </Suspense>
  );
}
