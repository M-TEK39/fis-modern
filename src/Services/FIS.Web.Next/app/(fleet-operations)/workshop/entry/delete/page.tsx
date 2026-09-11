import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { deleteWorkshopAction } from "@/app/(fleet-operations)/workshop/actions";
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

async function WorkshopEntryDeletePageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/workshop/entry/delete" />
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
  try {
    const [workshops, vehicles] = await Promise.all([getWorkshops(), getWorkshopVehicles()]);
    const vehicleByCode = new Map(vehicles.map((vehicle) => [vehicle.vmfCode, vehicle]));
    const selected =
      Number.isInteger(selectedCode) && selectedCode > 0
        ? workshops.find((entry) => entry.wwCode === selectedCode)
        : null;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="workshop-delete-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Workshop maintenance</p>
              <h1 id="workshop-delete-title">Delete Workshop Entry</h1>
              <p>Delete a workshop entry after confirming its vehicle and receive date.</p>
            </div>
            <Link className="button button-secondary" href="/workshop/entry">
              Back
            </Link>
          </header>
          {selected ? (
            <section
              className="vehicle-status-maintenance-panel"
              aria-labelledby="delete-selected-title"
            >
              <p className="eyebrow">Selected entry</p>
              <h2 id="delete-selected-title">Entry #{selected.wwCode}</h2>
              <p>
                {selected.vmfCode === null
                  ? "Vehicle not linked"
                  : vehicleByCode.get(selected.vmfCode)?.fleetNumber ||
                    `VMF ${selected.vmfCode}`}{" "}
                · received {formatDate(selected.receiveDate)}
              </p>
              <form action={deleteWorkshopAction}>
                <input name="returnPath" type="hidden" value="/workshop/entry/delete" />
                <input name="wwCode" type="hidden" value={selected.wwCode} />
                <div className="button-row">
                  <button className="button button-danger" type="submit">
                    Delete entry
                  </button>
                  <Link className="button button-secondary" href="/workshop/entry/delete">
                    Cancel
                  </Link>
                </div>
              </form>
            </section>
          ) : null}
          <section className="vehicle-status-maintenance-panel" aria-labelledby="delete-list-title">
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {workshops.length} record{workshops.length === 1 ? "" : "s"}
                </p>
                <h2 id="delete-list-title">Select a Workshop Entry</h2>
              </div>
            </div>
            {workshops.length === 0 ? (
              <p className="muted-copy">No workshop entries found.</p>
            ) : (
              <div className="vehicle-table-wrapper">
                <table className="vehicle-table">
                  <caption className="sr-only">Workshop entries available for deletion</caption>
                  <thead>
                    <tr>
                      <th scope="col">Entry</th>
                      <th scope="col">Vehicle</th>
                      <th scope="col">Received</th>
                      <th scope="col">Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {workshops.map((entry) => {
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
                          <td>{formatDate(entry.receiveDate)}</td>
                          <td>
                            <Link
                              className="button button-danger button-small"
                              href={`/workshop/entry/delete?id=${entry.wwCode}`}
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
              : "Workshop entries could not be loaded."}
          </h2>
          <Link className="button button-secondary" href="/workshop/entry">
            Back
          </Link>
        </section>
      </main>
    );
  }
}

export default function WorkshopEntryDeletePage(
  props: Parameters<typeof WorkshopEntryDeletePageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <WorkshopEntryDeletePageContent {...props} />
    </Suspense>
  );
}
