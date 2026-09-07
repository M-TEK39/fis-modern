import Link from "next/link";

import { JobCardTable, hasJobCardAccess, hasRole } from "@/app/job-cards/_components";
import { accessRestricted, filterByVehicle, getJobCardSession, queryValue, sessionMessage } from "@/app/job-cards/_page";
import { getJobCards, JobCardApiError } from "@/lib/api-job-cards";

export default async function CancelJobCardsPage({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  const problem = sessionMessage(session, "/job-cards/cancel");
  if (problem) return problem;
  if (session.status !== "authenticated") return accessRestricted("Your session could not be loaded.");
  if (!hasRole(session.roles, "capturer") && !hasJobCardAccess(session.accessLevel, session.roles)) return accessRestricted("Your profile does not include Job Card capturer access.");
  const query = await searchParams;
  const search = queryValue(query.search);
  const mode = queryValue(query.mode) || "GG";
  try {
    const cards = (await getJobCards()).filter((card) => [3, 4, 6, 7].includes(card.statusCode));
    const filtered = filterByVehicle(cards, search, mode);
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="cancel-job-cards-title"><header className="vehicle-page-header"><div><p className="eyebrow">Job Cards</p><h1 id="cancel-job-cards-title">Cancel Job Cards</h1><p>Search authorized or in-progress job cards and cancel them when required.</p></div><Link className="button button-secondary" href="/job-cards/capturer-default">Main menu</Link></header><section className="vehicle-status-maintenance-panel"><form className="vehicle-search-row" method="get"><fieldset className="vehicle-search-options"><legend>Find by</legend><label className="vehicle-checkbox-label"><input type="radio" name="mode" value="GG" defaultChecked={mode !== "GP"} /> GG</label><label className="vehicle-checkbox-label"><input type="radio" name="mode" value="GP" defaultChecked={mode === "GP"} /> GP</label></fieldset><label className="sr-only" htmlFor="cancel-job-card-search">Vehicle or job card</label><input className="vehicle-search" id="cancel-job-card-search" name="search" defaultValue={search} placeholder="GG, GP, or job card number" /><button className="button button-primary" type="submit">Search</button><Link className="button button-secondary" href="/job-cards/cancel">Clear</Link></form></section><section className="vehicle-status-maintenance-panel"><p className="eyebrow">{filtered.length} record{filtered.length === 1 ? "" : "s"}</p><h2>Cancelable Job Cards</h2><JobCardTable cards={filtered} mode="cancel" returnPath="/job-cards/cancel" /></section></section></main>;
  } catch (error) {
    const message = error instanceof JobCardApiError && error.reason === "unavailable" ? "The Job Cards service is temporarily unavailable. Please try again." : "Cancelable job cards could not be loaded.";
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><h2>{message}</h2><Link className="button button-secondary" href="/job-cards/cancel">Try again</Link></section></main>;
  }
}
