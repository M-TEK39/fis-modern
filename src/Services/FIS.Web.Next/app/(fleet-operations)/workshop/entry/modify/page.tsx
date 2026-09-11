import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";
import SearchTypeFieldset from "@/components/ui/search-type-fieldset";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { WorkshopEntryForm } from "@/app/(fleet-operations)/workshop/entry/workshop-entry-form";
import {
  getWorkshopVehicles,
  getWorkshops,
  WorkshopApiError,
  type WorkshopRecord,
  type WorkshopVehicle,
} from "@/lib/api/fleet-operations/api-workshop";
import { getSession } from "@/lib/auth/session";

function value(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

const WorkshopEntryModifyPageContent = renderWorkshopEntryModifyPageContent;

async function renderWorkshopEntryModifyPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/workshop/entry/modify" />
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
  const id = Number(value(query.id));
  const search = value(query.search) ?? "";
  const type = value(query.type) ?? "GG";
  try {
    const [allWorkshops, allVehicles] = await Promise.all([getWorkshops(), getWorkshopVehicles()]);
    const term = search.trim().toLocaleLowerCase();
    const options = term
      ? allVehicles.filter((vehicle) =>
          (type === "GP" ? vehicle.registrationNumber : vehicle.fleetNumber)
            ?.toLocaleLowerCase()
            .includes(term),
        )
      : allVehicles;
    const optionCodes = new Set(options.map((vehicle) => vehicle.vmfCode));
    const selected =
      Number.isInteger(id) && id > 0
        ? (allWorkshops.find((entry) => entry.wwCode === id) ?? null)
        : null;
    const selectedVehicles =
      selected?.vmfCode !== null &&
      selected?.vmfCode !== undefined &&
      !optionCodes.has(selected.vmfCode)
        ? [...options, allVehicles.find((vehicle) => vehicle.vmfCode === selected.vmfCode)].filter(
            (vehicle): vehicle is WorkshopVehicle => vehicle !== undefined,
          )
        : options;
    const workshopOptions = allWorkshops.reduce<WorkshopRecord[]>((result, entry) => {
      if (!term || (entry.vmfCode !== null && optionCodes.has(entry.vmfCode))) {
        result.push(entry);
      }
      return result;
    }, []);
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="workshop-modify-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Workshop maintenance</p>
              <h1 id="workshop-modify-title">Modify Workshop Entry</h1>
              <p>Search and edit a workshop entry.</p>
            </div>
            <Link className="button button-secondary" href="/workshop/entry">
              Back
            </Link>
          </header>
          <form className="vehicle-status-maintenance-panel" method="get">
            <SearchTypeFieldset selectedType={type} legend="Find entry by vehicle" name="type" />
            <div className="vehicle-search-row">
              <label className="sr-only" htmlFor="modify-workshop-search">
                Vehicle number
              </label>
              <input
                className="vehicle-search"
                id="modify-workshop-search"
                name="search"
                defaultValue={search}
                placeholder={type === "GP" ? "Enter GP number" : "Enter GG number"}
              />
              <button className="button button-secondary" type="submit">
                Find entries
              </button>
            </div>
            <label className="form-label" htmlFor="modify-workshop-id">
              Workshop Entry
            </label>
            <select
              className="form-select"
              id="modify-workshop-id"
              name="id"
              defaultValue={selected?.wwCode ?? ""}
            >
              <option value="">Select workshop entry...</option>
              {workshopOptions.map((entry) => (
                <option key={entry.wwCode} value={entry.wwCode}>
                  Entry #{entry.wwCode} - received {formatDate(entry.receiveDate)}
                </option>
              ))}
            </select>
          </form>
          {selected ? (
            <WorkshopEntryForm
              record={selected}
              vehicles={selectedVehicles}
              returnPath={`/workshop/entry/modify?id=${selected.wwCode}`}
            />
          ) : (
            <p className="muted-copy">Select a workshop entry to load its details.</p>
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

export default function WorkshopEntryModifyPage(
  props: Parameters<typeof WorkshopEntryModifyPageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <WorkshopEntryModifyPageContent {...props} />
    </Suspense>
  );
}
