import DataTableHeader from "@/components/ui/data-table-header";

import { connection } from "next/server";
import { redirect } from "next/navigation";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import ReportResultsPanel from "@/components/ui/report-results-panel";
import TroubleshootResultsTable from "@/components/ui/troubleshoot-results-table";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  DEFAULT_TROUBLESHOOT_PAGE_SIZE,
  TroubleshootApiError,
  getTroubleshootSiteUsers,
  searchTroubleshootLogsPage,
} from "@/lib/api/fleet-operations/api-troubleshoot";
import { getSession } from "@/lib/auth/session";
import {
  Pagination,
  StatusCard,
  TroubleshootMenu,
  TroubleshootShell,
} from "@/app/(fleet-operations)/troubleshoot/_components";
import { hasTroubleshootingRole, valueOrDash } from "@/app/(fleet-operations)/troubleshoot/_utils";
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

const TroubleshootLogPageContent = renderTroubleshootLogPageContent;

async function renderTroubleshootLogPageContent({
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
  const page = positive(first(query.page)) ?? 1;
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

  let pageData: Awaited<ReturnType<typeof searchTroubleshootLogsPage>> | null = null;
  let errorMessage: string | null = null;
  if (userAccessCode) {
    try {
      pageData = await searchTroubleshootLogsPage({
        userAccessCode,
        page,
        pageSize: DEFAULT_TROUBLESHOOT_PAGE_SIZE,
      });
    } catch (error) {
      errorMessage =
        error instanceof TroubleshootApiError
          ? error.message
          : "Troubleshoot logs could not be loaded.";
    }
  }

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
          <input type="hidden" name="page" value="1" />
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
      ) : errorMessage ? null : pageData?.total === 0 ? (
        <div className="vehicle-empty-state">
          <p>No logs match the selected user/site.</p>
        </div>
      ) : (
        <ReportResultsPanel
          headingId="troubleshoot-log-results-title"
          eyebrow={`${pageData?.total ?? 0} log${pageData?.total === 1 ? "" : "s"}`}
          heading="Report results"
          trailing={
            <form action={updateTroubleshootLogsAction}>
              <input type="hidden" name="userAccessCode" value={userAccessCode} />
              <button className="button button-secondary button-small" type="submit">
                Update Logs
              </button>
            </form>
          }
        >
          <TroubleshootResultsTable rows={pageData?.items ?? []} caption="Troubleshoot logs" />
          <Pagination
            path="/troubleshoot/log"
            page={pageData?.page ?? page}
            totalPages={pageData?.totalPages ?? 1}
            query={{ userAccessCode }}
          />
        </ReportResultsPanel>
      )}
    </TroubleshootShell>
  );
}

export default function TroubleshootLogPage(props: Readonly<{ searchParams: SearchParams }>) {
  return (
    <StreamedRoute>
      <TroubleshootLogPageContent {...props} />
    </StreamedRoute>
  );
}
