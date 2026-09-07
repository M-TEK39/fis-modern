import Link from "next/link";

import { LogbookMenu, LogbookShell, LogbookTable } from "@/app/log-books/_components";
import { accessRestricted, getLogbookSession, hasLogbookAccess, queryValue, sessionMessage } from "@/app/log-books/_page";
import { getLogbooks, LogbookApiError } from "@/lib/api-logbooks";

export default async function LogBooksPage({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLogbookSession();
  const problem = sessionMessage(session, "/log-books");
  if (problem) return problem;
  if (session.status !== "authenticated") return accessRestricted("Your session could not be loaded.");
  if (!hasLogbookAccess(session)) return accessRestricted("Your profile does not include Logbooks access.");

  const query = await searchParams;
  const search = queryValue(query.search).toLocaleLowerCase();
  try {
    const records = await getLogbooks();
    const filtered = records.filter((record) => !search || [record.vmfCode, record.ggNumber, record.registrationNumber, record.beginNumber, record.endNumber, record.receiverName, record.comment, record.siteDescription].some((value) => String(value ?? "").toLocaleLowerCase().includes(search)));
    return <LogbookShell title="Logbook Maintenance Menu" description="Manage vehicle logbook handouts while preserving the legacy workflow."><LogbookMenu /><section className="vehicle-status-maintenance-panel" aria-labelledby="logbook-preview-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">{filtered.length} of {records.length} record{records.length === 1 ? "" : "s"}</p><h2 id="logbook-preview-title">Logbook Handout Preview</h2></div></div><form className="vehicle-search-row" method="get"><label className="sr-only" htmlFor="logbook-preview-search">Search logbook handouts</label><input className="vehicle-search" id="logbook-preview-search" name="search" defaultValue={queryValue(query.search)} placeholder="Search GG, logbook range, receiver, comments" /><button className="button button-primary" type="submit">Filter</button><Link className="button button-secondary" href="/log-books">Clear</Link></form><LogbookTable records={filtered} mode="preview" returnPath="/log-books" /></section></LogbookShell>;
  } catch (error) {
    const message = error instanceof LogbookApiError && error.reason === "unavailable" ? "The Logbooks service is temporarily unavailable. Please try again." : "Logbooks could not be loaded.";
    return <LogbookShell title="Logbook Maintenance Menu" description="Manage vehicle logbook handouts while preserving the legacy workflow."><section className="vehicle-status-card" role="alert"><h2>{message}</h2><Link className="button button-secondary" href="/log-books">Try again</Link></section></LogbookShell>;
  }
}
