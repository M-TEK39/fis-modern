import Link from "next/link";

import { LicenseDetailsForm, LicenseHistoryTable, LicenseNotice, LicenseShell, LicenseVehicleSearch } from "@/app/licenses/_components";
import { saveLicenseVehicleAction } from "@/app/licenses/actions";
import { accessRestricted, getLicenseSession, hasLicenseAccess, queryValue, sessionMessage } from "@/app/licenses/_page";
import { getLicenseVehicle, getLicenseVehicleHistory, LicenseApiError } from "@/lib/api-licenses";
import { getSites, SiteApiError } from "@/lib/api-sites";

export default async function LicenseOneVehiclePage({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLicenseSession();
  const problem = sessionMessage(session, "/licenses/one-vehicle");
  if (problem) return problem;
  if (session.status !== "authenticated") return accessRestricted("Your session could not be loaded.");
  if (!hasLicenseAccess(session)) return accessRestricted();

  const query = await searchParams;
  const mode = queryValue(query.mode).toUpperCase() === "GP" ? "GP" : "GG";
  const number = queryValue(query.number).trim();
  const lookup = queryValue(query.lookup) === "1" && number.length > 0;
  const returnPath = `/licenses/one-vehicle?${new URLSearchParams({ mode, number, lookup: "1" }).toString()}`;
  let details = null;
  let history = null;
  let errorMessage = "";
  let sites: Awaited<ReturnType<typeof getSites>> = [];

  if (lookup) {
    try {
      const [loadedDetails, loadedSites] = await Promise.all([getLicenseVehicle(mode, number), getSites()]);
      details = loadedDetails;
      sites = loadedSites;
      history = await getLicenseVehicleHistory(loadedDetails.vmfCode);
    } catch (error) {
      errorMessage = error instanceof LicenseApiError || error instanceof SiteApiError ? "The Licence service is temporarily unavailable or the vehicle was not found. Check the number and try again." : "Licence details could not be loaded.";
    }
  }

  return <LicenseShell title="Licence Maintenance for ONE Vehicle" description="Capture and maintain the licence record for one vehicle."><LicenseNotice query={query} />{errorMessage ? <section className="vehicle-status-card" role="alert"><h2>{errorMessage}</h2><p className="muted-copy">The legacy record remains unchanged. Check the vehicle number and retry.</p></section> : null}<LicenseVehicleSearch mode={mode} number={number} />{details ? <><LicenseDetailsForm details={details} sites={sites} returnPath={returnPath} /><LicenseHistoryTable entries={history?.history ?? []} /></> : <p className="muted-copy">Search a vehicle to load its licence details and renewal history.</p>}<div className="vehicle-footer-actions"><Link className="button button-secondary" href="/licenses">Licence Menu</Link><Link className="button button-secondary" href="/licenses/one-vehicle">Clear</Link></div></LicenseShell>;
}
