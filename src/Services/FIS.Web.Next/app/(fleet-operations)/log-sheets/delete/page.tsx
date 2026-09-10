import Link from "next/link";

import {
  LogsheetShell,
  LogsheetTable,
  VehicleSearchForm,
} from "@/app/(fleet-operations)/log-sheets/_components";
import {
  accessRestricted,
  filterVehicles,
  getLogsheetSession,
  hasLogsheetAccess,
  queryValue,
  sessionMessage,
  statusMessage,
} from "@/app/(fleet-operations)/log-sheets/_page";
import { getLogsheets, LogsheetApiError } from "@/lib/api/fleet-operations/api-logsheets";
import { getVehicleOptions, VehicleApiError } from "@/lib/api/vehicles/api-vehicles";

export default async function LogsheetDeletePage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLogsheetSession();
  const problem = sessionMessage(session, "/log-sheets/delete");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLogsheetAccess(session))
    return accessRestricted("Your profile does not include Reports access.");
  if (!session.userAccessCode || ![279, 47, 38].includes(Number(session.userAccessCode)))
    return accessRestricted("Your profile cannot delete logsheets.");

  const query = await searchParams;
  const search = queryValue(query.search);
  const requisition = queryValue(query.requisition);
  const mode = queryValue(query.mode).toUpperCase() === "GP" ? "GP" : "GG";
  const vmfCode = Number(queryValue(query.vmfCode));
  const message = statusMessage(query);
  try {
    const [options, records] = await Promise.all([getVehicleOptions(), getLogsheets()]);
    const matches = filterVehicles(options, search, mode);
    const selectedVehicle =
      Number.isInteger(vmfCode) && vmfCode > 0
        ? options.find((vehicle) => vehicle.vmfCode === vmfCode)
        : null;
    const filtered = requisition
      ? records.filter(
          (record) =>
            record.requisitionNumber?.localeCompare(requisition, undefined, {
              sensitivity: "accent",
            }) === 0,
        )
      : selectedVehicle
        ? records.filter((record) => record.vmfCode === selectedVehicle.vmfCode)
        : [];
    const returnPath = `/log-sheets/delete?${new URLSearchParams({ ...(search ? { search } : {}), mode, ...(requisition ? { requisition } : {}), ...(selectedVehicle ? { vmfCode: String(vmfCode) } : {}) }).toString()}`;
    return (
      <LogsheetShell
        title="Delete a Logsheet"
        description="Search for a captured logsheet before removing it."
      >
        {message ? (
          <p
            className={`alert ${message.key === "error" ? "alert-error" : "alert-success"}`}
            role="status"
          >
            {message.value}
          </p>
        ) : null}
        <VehicleSearchForm
          action="/log-sheets/delete"
          search={search}
          mode={mode}
          vmfCode={selectedVehicle ? String(vmfCode) : ""}
          options={matches}
          requisition={requisition}
        />
        {filtered.length > 0 || requisition || selectedVehicle ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="logsheet-delete-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {filtered.length} record{filtered.length === 1 ? "" : "s"}
                </p>
                <h2 id="logsheet-delete-results-title">Matching logsheets</h2>
              </div>
            </div>
            <LogsheetTable records={filtered} mode="delete" returnPath={returnPath} />
          </section>
        ) : (
          <p className="muted-copy">Search by requisition or vehicle to load logsheets.</p>
        )}
        <div className="button-row">
          <Link className="button button-secondary" href="/log-sheets">
            Main menu
          </Link>
        </div>
      </LogsheetShell>
    );
  } catch (error) {
    const messageText =
      error instanceof LogsheetApiError || error instanceof VehicleApiError
        ? "The Logsheet service is temporarily unavailable. Please try again."
        : "Logsheets could not be loaded.";
    return (
      <LogsheetShell
        title="Delete a Logsheet"
        description="Search for a captured logsheet before removing it."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>{messageText}</h2>
          <Link className="button button-secondary" href="/log-sheets/delete">
            Try again
          </Link>
        </section>
      </LogsheetShell>
    );
  }
}
