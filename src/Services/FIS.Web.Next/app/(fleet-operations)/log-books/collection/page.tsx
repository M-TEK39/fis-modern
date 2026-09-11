import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import { collectLogbooksAction } from "@/app/(fleet-operations)/log-books/actions";
import { LogbookShell, VehicleSearchForm } from "@/app/(fleet-operations)/log-books/_components";
import { valueOrDash } from "@/app/(fleet-operations)/log-books/_utils";
import {
  accessRestricted,
  getLogbookSession,
  hasLogbookAccess,
  parsePositiveInteger,
  queryValue,
  queryValues,
  sessionMessage,
} from "@/app/(fleet-operations)/log-books/_page";
import { LogbookApiError } from "@/lib/api/fleet-operations/api-logbooks";
import { getSites } from "@/lib/api/reference-data/api-sites";
import { getVehicleOptions, type VehicleOption } from "@/lib/api/vehicles/api-vehicles";

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
  const key = ["saved", "error"].find((name) => query[name]);
  return key ? { key, value: queryValue(query[key]) } : null;
}

async function LogbookCollectionPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLogbookSession();
  const problem = sessionMessage(session, "/log-books/collection");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLogbookAccess(session))
    return accessRestricted("Your profile does not include Logbooks access.");

  const query = await searchParams;
  const search = queryValue(query.search);
  const mode = queryValue(query.mode) === "GP" ? "GP" : "GG";
  const selectedCodes = queryValues(query.vmfCode)
    .map(parsePositiveInteger)
    .filter((code): code is number => code !== null);
  const selectedCodeSet = new Set(selectedCodes);
  const message = statusMessage(query);
  try {
    const [options, sites] = await Promise.all([getVehicleOptions(), getSites()]);
    const matches = filterVehicles(options, search, mode);
    const searchParamsForForm = new URLSearchParams({ search, mode });
    selectedCodes.forEach((code) => searchParamsForForm.append("vmfCode", String(code)));
    return (
      <LogbookShell
        title="Collection for Two or More Logbooks"
        description="Create a handout for every selected vehicle in one collection workflow."
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
          action="/log-books/collection"
          search={search}
          mode={mode}
          vmfCode=""
          options={matches}
          multiple
          selectedVmfCodes={selectedCodes}
        />
        <form className="vehicle-status-maintenance-panel" action={collectLogbooksAction}>
          <input
            name="returnPath"
            type="hidden"
            value={`/log-books/collection?${searchParamsForForm.toString()}`}
          />
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">{selectedCodes.length} selected</p>
              <h2 id="collection-form-title">Collection details</h2>
            </div>
          </div>
          <div className="form-grid">
            <div className="form-field">
              <label className="form-label" htmlFor="collection-receiver">
                Receiver name
              </label>
              <input
                className="form-input"
                id="collection-receiver"
                name="receiverName"
                maxLength={25}
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="collection-telephone">
                Receiver telephone
              </label>
              <input
                className="form-input"
                id="collection-telephone"
                name="telephoneNumber"
                maxLength={20}
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="collection-site">
                Site
              </label>
              <select className="form-select" id="collection-site" name="siteCode">
                <option value="">Select site</option>
                {sites.map((site) => (
                  <option key={site.siteCode} value={site.siteCode}>
                    {site.description || "Unnamed site"} ({site.siteCode})
                  </option>
                ))}
              </select>
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="collection-date">
                Handout date
              </label>
              <input className="form-input" id="collection-date" name="handoutDate" type="date" />
            </div>
            <div className="form-field form-group-full">
              <label className="form-label" htmlFor="collection-comment">
                Comment
              </label>
              <textarea
                className="form-input"
                id="collection-comment"
                name="comment"
                maxLength={60}
                rows={3}
              />
            </div>
          </div>
          <fieldset>
            <legend>Selected vehicles</legend>
            {matches.length === 0 ? (
              <p className="muted-copy">Search by GG or GP number to load vehicles.</p>
            ) : (
              <div className="form-grid">
                {matches.map((vehicle) => (
                  <label className="form-checkbox" key={vehicle.vmfCode}>
                    <input
                      name="vmfCode"
                      type="checkbox"
                      value={vehicle.vmfCode}
                      defaultChecked={selectedCodeSet.has(vehicle.vmfCode)}
                    />
                    {valueOrDash(vehicle.fleetNumber)} / {valueOrDash(vehicle.registrationNumber)} (
                    {vehicle.vmfCode})
                  </label>
                ))}
              </div>
            )}
          </fieldset>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Submit collection
            </button>
            <Link className="button button-secondary" href="/log-books">
              Main menu
            </Link>
          </div>
        </form>
      </LogbookShell>
    );
  } catch (error) {
    const messageText =
      error instanceof LogbookApiError
        ? "The Logbooks service is temporarily unavailable. Please try again."
        : "Logbook collection could not be loaded.";
    return (
      <LogbookShell
        title="Collection for Two or More Logbooks"
        description="Create a handout for every selected vehicle in one collection workflow."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>{messageText}</h2>
          <Link className="button button-secondary" href="/log-books/collection">
            Try again
          </Link>
        </section>
      </LogbookShell>
    );
  }
}

export default function LogbookCollectionPage(
  props: Parameters<typeof LogbookCollectionPageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LogbookCollectionPageContent {...props} />
    </Suspense>
  );
}
