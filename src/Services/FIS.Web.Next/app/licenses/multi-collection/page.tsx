import Link from "next/link";

import { LicenseDetailsForm, LicenseHistoryTable, LicenseNotice, LicenseShell, LicenseVehicleSearch } from "@/app/licenses/_components";
import { accessRestricted, getLicenseSession, hasLicenseAccess, queryValue, sessionMessage } from "@/app/licenses/_page";
import { getLicenseVehicle, getLicenseVehicleHistory, LicenseApiError } from "@/lib/api-licenses";
import { getSites, SiteApiError } from "@/lib/api-sites";

function buildPath(mode: "GG" | "GP", number: string, receiver: string, receiverId: string, receiverTel: string, dateCollected: string) {
  const params = new URLSearchParams({ mode, number, lookup: "1" });
  if (receiver) params.set("receiver", receiver);
  if (receiverId) params.set("receiverId", receiverId);
  if (receiverTel) params.set("receiverTel", receiverTel);
  if (dateCollected) params.set("dateCollected", dateCollected);
  return `/licenses/multi-collection?${params.toString()}`;
}

export default async function LicenseMultiCollectionPage({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLicenseSession();
  const problem = sessionMessage(session, "/licenses/multi-collection");
  if (problem) return problem;
  if (session.status !== "authenticated") return accessRestricted("Your session could not be loaded.");
  if (!hasLicenseAccess(session)) return accessRestricted();

  const query = await searchParams;
  const mode = queryValue(query.mode).toUpperCase() === "GP" ? "GP" : "GG";
  const number = queryValue(query.number).trim();
  const receiver = queryValue(query.receiver).trim();
  const receiverId = queryValue(query.receiverId).trim();
  const receiverTel = queryValue(query.receiverTel).trim();
  const dateCollected = queryValue(query.dateCollected).trim();
  const lookup = queryValue(query.lookup) === "1" && number.length > 0;
  const returnPath = buildPath(mode, number, receiver, receiverId, receiverTel, dateCollected);
  let details = null;
  let history = null;
  let sites: Awaited<ReturnType<typeof getSites>> = [];
  let errorMessage = "";

  if (lookup) {
    try {
      const [loadedDetails, loadedSites] = await Promise.all([getLicenseVehicle(mode, number), getSites()]);
      details = {
        ...loadedDetails,
        receiver: receiver || loadedDetails.receiver,
        receiverId: receiverId || loadedDetails.receiverId,
        receiverTel: receiverTel || loadedDetails.receiverTel,
        dateCollected: dateCollected || loadedDetails.dateCollected,
      };
      sites = loadedSites;
      history = await getLicenseVehicleHistory(loadedDetails.vmfCode);
    } catch (error) {
      errorMessage = error instanceof LicenseApiError || error instanceof SiteApiError
        ? "The Licence service is temporarily unavailable or the vehicle was not found. Check the number and try again."
        : "Licence details could not be loaded.";
    }
  }

  return <LicenseShell title="Collection for TWO or MORE Licences" description="Collect several vehicle licences for one receiver. Complete the receiver details, then process each vehicle in turn."><LicenseNotice query={query} />{errorMessage ? <section className="vehicle-status-card" role="alert"><h2>{errorMessage}</h2><p className="muted-copy">The legacy record remains unchanged. Check the vehicle number and retry.</p></section> : null}<section className="vehicle-status-maintenance-panel" aria-labelledby="multi-receiver-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Collection details</p><h2 id="multi-receiver-title">Receiver Details</h2></div></div><form method="get"><div className="form-grid"><div className="form-field"><label className="form-label" htmlFor="multi-receiver">Receiver Name</label><input className="form-input" id="multi-receiver" name="receiver" defaultValue={receiver} maxLength={20} required /></div><div className="form-field"><label className="form-label" htmlFor="multi-receiver-id">ID Number</label><input className="form-input" id="multi-receiver-id" name="receiverId" defaultValue={receiverId} maxLength={13} required /></div><div className="form-field"><label className="form-label" htmlFor="multi-receiver-tel">Tel Number</label><input className="form-input" id="multi-receiver-tel" name="receiverTel" defaultValue={receiverTel} maxLength={16} required /></div><div className="form-field"><label className="form-label" htmlFor="multi-date-collected">Date Collected</label><input className="form-input" id="multi-date-collected" name="dateCollected" type="date" defaultValue={dateCollected} required /></div></div><input name="mode" type="hidden" value={mode} />{number ? <input name="number" type="hidden" value={number} /> : null}{lookup ? <input name="lookup" type="hidden" value="1" /> : null}<div className="button-row"><button className="button button-primary" type="submit">Save receiver details</button><Link className="button button-secondary" href="/licenses/multi-collection">Clear</Link></div></form></section><LicenseVehicleSearch mode={mode} number={number} clearHref="/licenses/multi-collection" hiddenFields={{ receiver, receiverId, receiverTel, dateCollected }} />{details ? <><LicenseDetailsForm details={details} sites={sites} returnPath={returnPath} workflow="multi-collection" /><LicenseHistoryTable entries={history?.history ?? []} /></> : <p className="muted-copy">Enter receiver details and search a vehicle to load its licence record.</p>}<div className="vehicle-footer-actions"><Link className="button button-secondary" href="/licenses">Licence Menu</Link><Link className="button button-secondary" href="/licenses/multi-collection">Clear</Link></div></LicenseShell>;
}
