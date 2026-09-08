import Link from "next/link";

import { LogbookShell, LogbookTable, VehicleSearchForm } from "@/app/log-books/_components";
import {
  accessRestricted,
  getLogbookSession,
  hasLogbookAccess,
  parsePositiveInteger,
  queryValue,
  sessionMessage,
} from "@/app/log-books/_page";
import { getLogbooks, LogbookApiError } from "@/lib/api-logbooks";
import { getVehicleOptions, type VehicleOption } from "@/lib/api-vehicles";

function filterVehicles(options: readonly VehicleOption[], search: string, mode: string) {
  const normalized = search.trim().toLocaleLowerCase();
  if (!normalized) return [];
  const isGp = mode === "GP";
  return options
    .filter((vehicle) =>
      (isGp ? vehicle.registrationNumber : vehicle.fleetNumber)
        ?.toLocaleLowerCase()
        .includes(normalized),
    )
    .sort((left, right) => left.vmfCode - right.vmfCode);
}

function statusMessage(query: Record<string, string | string[] | undefined>) {
  const key = ["deleted", "error"].find((name) => query[name]);
  return key ? { key, value: queryValue(query[key]) } : null;
}

export default async function LogbookDeletePage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLogbookSession();
  const problem = sessionMessage(session, "/log-books/delete");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLogbookAccess(session))
    return accessRestricted("Your profile does not include Logbooks access.");

  const query = await searchParams;
  const search = queryValue(query.search);
  const mode = queryValue(query.mode) === "GP" ? "GP" : "GG";
  const vmfCode = parsePositiveInteger(queryValue(query.vmfCode));
  const params = new URLSearchParams();
  if (search) params.set("search", search);
  params.set("mode", mode);
  if (vmfCode) params.set("vmfCode", String(vmfCode));
  const returnPath = `/log-books/delete?${params.toString()}`;
  const message = statusMessage(query);
  try {
    const [options, records] = await Promise.all([getVehicleOptions(), getLogbooks()]);
    const matches = filterVehicles(options, search, mode);
    const selectedRecords = vmfCode ? records.filter((record) => record.vmfCode === vmfCode) : [];
    return (
      <LogbookShell
        title="Delete a Logbook Handout"
        description="Find a vehicle and delete a returned logbook handout."
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
          action="/log-books/delete"
          search={search}
          mode={mode}
          vmfCode={vmfCode?.toString() ?? ""}
          options={matches}
        />
        {vmfCode ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="logbook-delete-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {selectedRecords.length} record{selectedRecords.length === 1 ? "" : "s"}
                </p>
                <h2 id="logbook-delete-results-title">Handouts for selected vehicle</h2>
              </div>
            </div>
            <LogbookTable records={selectedRecords} mode="delete" returnPath={returnPath} />
          </section>
        ) : (
          <p className="muted-copy">
            Search for a vehicle, then select it to load handouts for deletion.
          </p>
        )}
        <Link className="button button-secondary" href="/log-books">
          Main menu
        </Link>
      </LogbookShell>
    );
  } catch (error) {
    const messageText =
      error instanceof LogbookApiError
        ? "The Logbooks service is temporarily unavailable. Please try again."
        : "Logbook handouts could not be loaded.";
    return (
      <LogbookShell
        title="Delete a Logbook Handout"
        description="Find a vehicle and delete a returned logbook handout."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>{messageText}</h2>
          <Link className="button button-secondary" href={returnPath}>
            Try again
          </Link>
        </section>
      </LogbookShell>
    );
  }
}
