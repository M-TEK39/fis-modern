import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import {
  LogsheetForm,
  LogsheetShell,
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
import { getSites, SiteApiError } from "@/lib/api/reference-data/api-sites";
import { LogsheetApiError } from "@/lib/api/fleet-operations/api-logsheets";
import { getVehicleOptions, VehicleApiError } from "@/lib/api/vehicles/api-vehicles";

async function LogsheetEntryPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLogsheetSession();
  const problem = sessionMessage(session, "/log-sheets/enter");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLogsheetAccess(session))
    return accessRestricted("Your profile does not include Reports access.");

  const query = await searchParams;
  const search = queryValue(query.search);
  const mode = queryValue(query.mode).toUpperCase() === "GP" ? "GP" : "GG";
  const vmfCode = Number(queryValue(query.vmfCode));
  const message = statusMessage(query);
  try {
    const [options, sites] = await Promise.all([getVehicleOptions(), getSites()]);
    const matches = filterVehicles(options, search, mode);
    const selected =
      Number.isInteger(vmfCode) && vmfCode > 0
        ? options.find((vehicle) => vehicle.vmfCode === vmfCode)
        : null;
    const returnPath = `/log-sheets/enter?${new URLSearchParams({ ...(search ? { search } : {}), mode, ...(selected ? { vmfCode: String(vmfCode) } : {}) }).toString()}`;
    return (
      <LogsheetShell
        title="Enter a Logsheet"
        description="Capture monthly vehicle usage against the existing FIS logsheet schema."
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
          action="/log-sheets/enter"
          search={search}
          mode={mode}
          vmfCode={selected ? String(vmfCode) : ""}
          options={matches}
        />
        {selected ? (
          <LogsheetForm
            record={null}
            vmfCode={selected.vmfCode}
            sites={sites}
            returnPath={returnPath}
          />
        ) : (
          <p className="muted-copy">
            Search by GG or GP number, choose the matching vehicle, and select Find to continue.
          </p>
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
      error instanceof LogsheetApiError ||
      error instanceof VehicleApiError ||
      error instanceof SiteApiError
        ? "The Logsheet service is temporarily unavailable. Please try again."
        : "Logsheet entry could not be loaded.";
    return (
      <LogsheetShell
        title="Enter a Logsheet"
        description="Capture monthly vehicle usage against the existing FIS logsheet schema."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>{messageText}</h2>
          <Link className="button button-secondary" href="/log-sheets/enter">
            Try again
          </Link>
        </section>
      </LogsheetShell>
    );
  }
}

export default function LogsheetEntryPage(props: Parameters<typeof LogsheetEntryPageContent>[0]) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LogsheetEntryPageContent {...props} />
    </Suspense>
  );
}
