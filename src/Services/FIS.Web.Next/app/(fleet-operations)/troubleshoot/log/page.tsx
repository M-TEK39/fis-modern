import { connection } from "next/server";
import { redirect } from "next/navigation";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  TroubleshootApiError,
  getTroubleshootSiteUsers,
  searchTroubleshootLogs,
} from "@/lib/api/fleet-operations/api-troubleshoot";
import { getSession } from "@/lib/auth/session";
import {
  hasTroubleshootingRole,
  Pagination,
  StatusCard,
  TroubleshootMenu,
  TroubleshootShell,
  valueOrDash,
} from "@/app/(fleet-operations)/troubleshoot/_components";
import { updateTroubleshootLogsAction } from "@/app/(fleet-operations)/troubleshoot/actions";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
function positive(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

function dateValue(value: string | null) {
  return value ? value.slice(0, 10) : "-";
}

export default async function TroubleshootLogPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/troubleshoot/log" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="API unavailable"
          message="The sign-in service is temporarily unavailable."
          href="/troubleshoot/log"
        />
      </main>
    );
  if (!hasTroubleshootingRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="Access restricted"
          message="You do not have permission to access Troubleshoot."
          href="/home"
        />
      </main>
    );

  const query = await searchParams;
  const userAccessCode = positive(first(query.userAccessCode));
  const page = Number(first(query.page)) > 0 ? Number(first(query.page)) : 1;
  let siteUsers;
  try {
    siteUsers = await getTroubleshootSiteUsers();
  } catch (error) {
    console.error(
      "FIS troubleshoot site user lookup failed",
      error instanceof TroubleshootApiError ? error.message : "unknown error",
    );
    return (
      <TroubleshootShell
        title="Troubleshoot Log"
        description="View and maintain troubleshoot log entries."
      >
        <TroubleshootMenu />
        <StatusCard
          title="API unavailable"
          message="The department/site list could not be loaded."
          href="/troubleshoot/log"
        />
      </TroubleshootShell>
    );
  }

  let logs = [] as Awaited<ReturnType<typeof searchTroubleshootLogs>>;
  let errorMessage: string | null = null;
  if (userAccessCode) {
    try {
      logs = await searchTroubleshootLogs(userAccessCode);
    } catch (error) {
      errorMessage =
        error instanceof TroubleshootApiError
          ? error.message
          : "Troubleshoot logs could not be loaded.";
    }
  }

  const pageSize = 12;
  const totalPages = Math.max(1, Math.ceil(logs.length / pageSize));
  const currentPage = Math.min(page, totalPages);
  const visibleLogs = logs.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  const saved = first(query.saved);

  return (
    <TroubleshootShell
      title="Troubleshoot Log"
      description="View and maintain troubleshoot log entries."
    >
      <TroubleshootMenu />
      <section
        className="vehicle-status-maintenance-panel"
        aria-labelledby="troubleshoot-log-search-title"
      >
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Search criteria</p>
            <h2 id="troubleshoot-log-search-title">Select: Department/Site</h2>
          </div>
        </div>
        <form className="vehicle-create-form" method="get">
          <label className="form-label" htmlFor="troubleshoot-log-user">
            User/site
          </label>
          <select
            className="form-select"
            id="troubleshoot-log-user"
            name="userAccessCode"
            defaultValue={userAccessCode ?? ""}
            required
          >
            <option value="">Select user/site</option>
            {siteUsers.map((option) => (
              <option key={option.userAccessCode} value={option.userAccessCode}>
                {option.name || "Unknown User"} ({option.userAccessCode}) -{" "}
                {valueOrDash(option.siteDescription)} ({valueOrDash(option.siteCode)})
              </option>
            ))}
          </select>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Next
            </button>
          </div>
        </form>
      </section>
      {saved ? (
        <div className="notice notice-success" role="status">
          Troubleshoot logs updated ({saved} record{saved === "1" ? "" : "s"}).
        </div>
      ) : null}
      {errorMessage ? (
        <div className="notice notice-error" role="alert">
          {errorMessage}
        </div>
      ) : null}
      {!userAccessCode ? (
        <div className="vehicle-empty-state">
          <p>Select a user/site to load its troubleshoot logs.</p>
        </div>
      ) : errorMessage ? null : logs.length === 0 ? (
        <div className="vehicle-empty-state">
          <p>No logs match the selected user/site.</p>
        </div>
      ) : (
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="troubleshoot-log-results-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">
                {logs.length} log{logs.length === 1 ? "" : "s"}
              </p>
              <h2 id="troubleshoot-log-results-title">Report results</h2>
            </div>
            <form action={updateTroubleshootLogsAction}>
              <input type="hidden" name="userAccessCode" value={userAccessCode} />
              <button className="button button-secondary button-small" type="submit">
                Update Logs
              </button>
            </form>
          </div>
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">Troubleshoot logs</caption>
              <thead>
                <tr>
                  <th scope="col">Vehicle</th>
                  <th scope="col">Description</th>
                  <th scope="col">Status</th>
                  <th scope="col">Logged date</th>
                  <th scope="col">Logged by</th>
                </tr>
              </thead>
              <tbody>
                {visibleLogs.map((log) => (
                  <tr key={log.id}>
                    <td>{valueOrDash(log.vehicleIdentifier)}</td>
                    <td>{valueOrDash(log.problemDescription)}</td>
                    <td>{valueOrDash(log.status)}</td>
                    <td>{dateValue(log.loggedDate)}</td>
                    <td>{valueOrDash(log.loggedBy)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination
            path="/troubleshoot/log"
            page={currentPage}
            totalPages={totalPages}
            query={{ userAccessCode }}
          />
        </section>
      )}
    </TroubleshootShell>
  );
}
