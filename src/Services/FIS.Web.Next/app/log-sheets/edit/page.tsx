import Link from "next/link";

import {
  LogsheetForm,
  LogsheetShell,
  LogsheetTable,
  VehicleSearchForm,
} from "@/app/log-sheets/_components";
import {
  accessRestricted,
  filterVehicles,
  getLogsheetSession,
  hasLogsheetAccess,
  queryValue,
  sessionMessage,
  statusMessage,
} from "@/app/log-sheets/_page";
import { getLogsheets, LogsheetApiError, type LogsheetRecord } from "@/lib/api-logsheets";
import { getSites, SiteApiError } from "@/lib/api-sites";
import { getVehicleOptions, VehicleApiError } from "@/lib/api-vehicles";

function findRecords(records: readonly LogsheetRecord[], requisition: string, vmfCode: number) {
  if (requisition)
    return records.filter(
      (record) =>
        record.requisitionNumber?.localeCompare(requisition, undefined, {
          sensitivity: "accent",
        }) === 0,
    );
  return vmfCode > 0 ? records.filter((record) => record.vmfCode === vmfCode) : [];
}

export default async function LogsheetEditPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLogsheetSession();
  const problem = sessionMessage(session, "/log-sheets/edit");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLogsheetAccess(session))
    return accessRestricted("Your profile does not include Reports access.");
  if (!session.userAccessCode || ![279, 47, 38].includes(Number(session.userAccessCode)))
    return accessRestricted("Your profile cannot edit logsheets.");

  const query = await searchParams;
  const search = queryValue(query.search);
  const requisition = queryValue(query.requisition);
  const mode = queryValue(query.mode).toUpperCase() === "GP" ? "GP" : "GG";
  const vmfCode = Number(queryValue(query.vmfCode));
  const editCode = Number(queryValue(query.edit));
  const message = statusMessage(query);
  try {
    const [options, sites, records] = await Promise.all([
      getVehicleOptions(),
      getSites(),
      getLogsheets(),
    ]);
    const matches = filterVehicles(options, search, mode);
    const selectedVehicle =
      Number.isInteger(vmfCode) && vmfCode > 0
        ? options.find((vehicle) => vehicle.vmfCode === vmfCode)
        : null;
    const selectedRecords = findRecords(records, requisition, vmfCode);
    const editing =
      Number.isInteger(editCode) && editCode > 0
        ? (selectedRecords.find((record) => record.logCode === editCode) ?? null)
        : null;
    const params = new URLSearchParams({
      ...(search ? { search } : {}),
      mode,
      ...(requisition ? { requisition } : {}),
      ...(selectedVehicle ? { vmfCode: String(vmfCode) } : {}),
    });
    const returnPath = `/log-sheets/edit?${params.toString()}`;
    return (
      <LogsheetShell
        title="Edit Logsheets"
        description="Search by requisition or vehicle, then update captured values."
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
          action="/log-sheets/edit"
          search={search}
          mode={mode}
          vmfCode={selectedVehicle ? String(vmfCode) : ""}
          options={matches}
          requisition={requisition}
        />
        {selectedRecords.length > 0 || requisition || selectedVehicle ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="logsheet-edit-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {selectedRecords.length} record{selectedRecords.length === 1 ? "" : "s"}
                </p>
                <h2 id="logsheet-edit-results-title">Matching logsheets</h2>
              </div>
            </div>
            <LogsheetTable records={selectedRecords} mode="edit" returnPath={returnPath} />
          </section>
        ) : (
          <p className="muted-copy">Search by requisition or vehicle to load logsheets.</p>
        )}
        {editing ? (
          <LogsheetForm
            record={editing}
            vmfCode={editing.vmfCode}
            sites={sites}
            returnPath={returnPath}
          />
        ) : null}
        <div className="button-row">
          <Link className="button button-secondary" href="/log-sheets">
            Main menu
          </Link>
        </div>
      </LogsheetShell>
    );
  } catch (error) {
    const messageText =
      error instanceof LogsheetApiError ||
      error instanceof VehicleApiError ||
      error instanceof SiteApiError
        ? "The Logsheet service is temporarily unavailable. Please try again."
        : "Logsheets could not be loaded.";
    return (
      <LogsheetShell
        title="Edit Logsheets"
        description="Search by requisition or vehicle, then update captured values."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>{messageText}</h2>
          <Link className="button button-secondary" href="/log-sheets/edit">
            Try again
          </Link>
        </section>
      </LogsheetShell>
    );
  }
}
