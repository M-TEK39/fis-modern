import Link from "next/link";

import { MonitorMenu, MonitorNotice, MonitorShell, MonitorTable } from "@/app/monitor/_components";
import {
  accessRestricted,
  getMonitorSession,
  hasCallCentreAccess,
  queryValue,
  sessionMessage,
} from "@/app/monitor/_page";
import { getMonitors, MonitorApiError } from "@/lib/api-monitor";

export default async function MonitorPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getMonitorSession();
  const problem = sessionMessage(session, "/monitor");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasCallCentreAccess(session))
    return accessRestricted("Your profile does not include Call Centre access.");

  const query = await searchParams;
  const search = queryValue(query.search).toLocaleLowerCase();
  try {
    const records = (await getMonitors()).filter((record) => !record.isDeleted);
    const filtered = records.filter(
      (record) =>
        !search ||
        [
          record.monitorCode,
          record.vmfCode,
          record.fleetNumber,
          record.registrationNumber,
          record.inquiryType,
          record.inquiryDescription,
          record.driverName,
          record.driverPersalNo,
          record.driverSite,
        ].some((value) =>
          String(value ?? "")
            .toLocaleLowerCase()
            .includes(search),
        ),
    );
    return (
      <MonitorShell
        title="Monitor Maintenance Menu"
        description="Capture and manage Call Centre monitoring inquiries while preserving the legacy workflow."
      >
        <MonitorNotice query={query} />
        <MonitorMenu />
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="monitor-preview-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">
                {filtered.length} of {records.length} active inquiries
              </p>
              <h2 id="monitor-preview-title">Monitor Inquiry Preview</h2>
            </div>
          </div>
          <form className="vehicle-search-row" method="get">
            <label className="sr-only" htmlFor="monitor-preview-search">
              Search inquiries
            </label>
            <input
              className="vehicle-search"
              id="monitor-preview-search"
              name="search"
              defaultValue={queryValue(query.search)}
              placeholder="Search reference, vehicle, driver, type"
            />
            <button className="button button-primary" type="submit">
              Filter
            </button>
            <Link className="button button-secondary" href="/monitor">
              Clear
            </Link>
          </form>
          <MonitorTable records={filtered.slice(0, 100)} />
        </section>
      </MonitorShell>
    );
  } catch (error) {
    return (
      <MonitorShell
        title="Monitor Maintenance Menu"
        description="Capture and manage Call Centre monitoring inquiries while preserving the legacy workflow."
      >
        <MonitorMenu />
        <section className="vehicle-status-card" role="alert">
          <h2>
            {error instanceof MonitorApiError && error.reason === "unavailable"
              ? "The Monitor service is temporarily unavailable."
              : "Monitor inquiries could not be loaded."}
          </h2>
          <p className="muted-copy">
            The maintenance menu is available while the service is restored.
          </p>
          <Link className="button button-primary" href="/monitor">
            Try again
          </Link>
        </section>
      </MonitorShell>
    );
  }
}
