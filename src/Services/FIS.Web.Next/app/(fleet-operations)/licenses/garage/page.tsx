import Link from "next/link";

import {
  LicenseDetailsForm,
  LicenseHistoryTable,
  LicenseNotice,
  LicenseShell,
  LicenseVehicleSearch,
} from "@/app/(fleet-operations)/licenses/_components";
import {
  accessRestricted,
  getLicenseSession,
  hasLicenseAccess,
  queryValue,
  sessionMessage,
} from "@/app/(fleet-operations)/licenses/_page";
import {
  getLicenseVehicle,
  getLicenseVehicleHistory,
  LicenseApiError,
} from "@/lib/api/vehicles/api-licenses";
import { getSites, SiteApiError } from "@/lib/api/reference-data/api-sites";

function addOneYear(value: string | null) {
  const date = value?.slice(0, 10);
  if (!date || !/^\d{4}-\d{2}-\d{2}$/.test(date)) return value;
  const [year, month, day] = date.split("-").map(Number);
  const targetYear = year + 1;
  const lastDay = new Date(Date.UTC(targetYear, month, 0)).getUTCDate();
  return `${targetYear}-${String(month).padStart(2, "0")}-${String(Math.min(day, lastDay)).padStart(2, "0")}`;
}

function buildPath(mode: "GG" | "GP", number: string) {
  return `/licenses/garage?${new URLSearchParams({ mode, number, lookup: "1" }).toString()}`;
}

export default async function LicenseGaragePage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLicenseSession();
  const problem = sessionMessage(session, "/licenses/garage");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLicenseAccess(session)) return accessRestricted();

  const query = await searchParams;
  const mode = queryValue(query.mode).toUpperCase() === "GP" ? "GP" : "GG";
  const number = queryValue(query.number).trim();
  const lookup = queryValue(query.lookup) === "1" && number.length > 0;
  const returnPath = buildPath(mode, number);
  let details = null;
  let history = null;
  let sites: Awaited<ReturnType<typeof getSites>> = [];
  let errorMessage = "";

  if (lookup) {
    try {
      const [loadedDetails, loadedSites] = await Promise.all([
        getLicenseVehicle(mode, number),
        getSites(),
      ]);
      details = { ...loadedDetails, expDate: addOneYear(loadedDetails.expDate) };
      sites = loadedSites;
      history = await getLicenseVehicleHistory(loadedDetails.vmfCode);
    } catch (error) {
      errorMessage =
        error instanceof LicenseApiError || error instanceof SiteApiError
          ? "The Licence service is temporarily unavailable or the vehicle was not found. Check the number and try again."
          : "Licence details could not be loaded.";
    }
  }

  return (
    <LicenseShell
      title="Licence At Garage Maintenance"
      description="Capture a licence that is available at the garage."
    >
      <LicenseNotice query={query} />
      {errorMessage ? (
        <section className="vehicle-status-card" role="alert">
          <h2>{errorMessage}</h2>
          <p className="muted-copy">
            The legacy record remains unchanged. Check the vehicle number and retry.
          </p>
        </section>
      ) : null}
      <section className="vehicle-status-card">
        <p className="eyebrow">Legacy workflow rule</p>
        <p className="muted-copy">
          The Exp Date is preloaded one year after the current licence date, matching the garage
          collection workflow. Review it before saving.
        </p>
      </section>
      <LicenseVehicleSearch mode={mode} number={number} clearHref="/licenses/garage" />
      {details ? (
        <>
          <LicenseDetailsForm
            details={details}
            sites={sites}
            returnPath={returnPath}
            workflow="garage"
          />
          <LicenseHistoryTable entries={history?.history ?? []} />
        </>
      ) : (
        <p className="muted-copy">Search a vehicle to load its licence details.</p>
      )}
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/licenses">
          Licence Menu
        </Link>
        <Link className="button button-secondary" href="/licenses/garage">
          Clear
        </Link>
      </div>
    </LicenseShell>
  );
}
