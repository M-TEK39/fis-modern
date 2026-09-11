import { redirect } from "next/navigation";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";
import {
  DEFAULT_TROUBLESHOOT_PAGE_SIZE,
  getTroubleshootReportsPage,
  getTroubleshootUsers,
  TroubleshootApiError,
} from "@/lib/api/fleet-operations/api-troubleshoot";
import {
  hasTroubleshootingRole,
  Pagination,
  pageNumber,
  StatusCard,
  TroubleshootMenu,
  TroubleshootShell,
  UserLabel,
  valueOrDash,
} from "@/app/(fleet-operations)/troubleshoot/_components";

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

function dateInput(value: string | undefined) {
  return value && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : "";
}

async function TroubleshootReportsPageContent({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/troubleshoot/reports" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="API unavailable"
          message="The sign-in service is temporarily unavailable."
          href="/troubleshoot/reports"
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
  const problemKeyword = first(query.problemKeyword) ?? "";
  const userAccessCode = positive(first(query.userAccessCode));
  const fromDate = first(query.fromDate);
  const toDate = first(query.toDate);
  const submitted = first(query.submitted) === "1";
  const openInExcel = first(query.openInExcel) === "1";
  const page = pageNumber(query.page);
  let users;
  try {
    users = await getTroubleshootUsers();
  } catch (error) {
    console.error(
      "FIS troubleshoot report user lookup failed",
      error instanceof TroubleshootApiError ? error.message : "unknown error",
    );
    return (
      <TroubleshootShell
        title="Troubleshoot General Reports"
        description="Troubleshoot reporting workflow."
      >
        <TroubleshootMenu />
        <StatusCard
          title="API unavailable"
          message="Troubleshoot users could not be loaded."
          href="/troubleshoot/reports"
        />
      </TroubleshootShell>
    );
  }

  let pageData: Awaited<ReturnType<typeof getTroubleshootReportsPage>> | null = null;
  let errorMessage: string | null = null;
  if (submitted) {
    if ((fromDate && !dateInput(fromDate)) || (toDate && !dateInput(toDate))) {
      errorMessage = "Enter valid From and To dates.";
    } else if (fromDate && toDate && fromDate > toDate) {
      errorMessage = "The To date must be on or after the From date.";
    } else {
      try {
        pageData = await getTroubleshootReportsPage({
          problemKeyword,
          userAccessCode: userAccessCode ?? undefined,
          fromDate,
          toDate,
          openInExcel,
          page,
          pageSize: DEFAULT_TROUBLESHOOT_PAGE_SIZE,
        });
      } catch (error) {
        errorMessage =
          error instanceof TroubleshootApiError
            ? error.message
            : "Troubleshoot report could not be loaded.";
      }
    }
  }

  return (
    <TroubleshootShell
      title="Troubleshoot General Reports"
      description="Troubleshoot reporting workflow."
    >
      <TroubleshootMenu />
      <section
        className="vehicle-status-maintenance-panel"
        aria-labelledby="troubleshoot-report-filter-title"
      >
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Report filters</p>
            <h2 id="troubleshoot-report-filter-title">Search troubleshoot records</h2>
          </div>
        </div>
        <form className="vehicle-create-form" method="get">
          <input type="hidden" name="submitted" value="1" />
          <input type="hidden" name="page" value="1" />
          <div className="form-grid">
            <div className="form-field">
              <label className="form-label" htmlFor="troubleshoot-report-keyword">
                Problem description keyword
              </label>
              <input
                className="form-input"
                id="troubleshoot-report-keyword"
                name="problemKeyword"
                defaultValue={problemKeyword}
                placeholder="Keyword(s)"
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="troubleshoot-report-user">
                User
              </label>
              <select
                className="form-select"
                id="troubleshoot-report-user"
                name="userAccessCode"
                defaultValue={userAccessCode ?? ""}
              >
                <option value="">ALL USERS</option>
                {users.map((user) => (
                  <option key={user.userAccessCode} value={user.userAccessCode}>
                    <UserLabel user={user} />
                  </option>
                ))}
              </select>
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="troubleshoot-report-from">
                From
              </label>
              <input
                className="form-input"
                id="troubleshoot-report-from"
                type="date"
                name="fromDate"
                defaultValue={dateInput(fromDate)}
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="troubleshoot-report-to">
                To
              </label>
              <input
                className="form-input"
                id="troubleshoot-report-to"
                type="date"
                name="toDate"
                defaultValue={dateInput(toDate)}
              />
            </div>
            <div className="form-field form-group-full">
              <label className="form-checkbox" htmlFor="troubleshoot-report-excel">
                <input
                  id="troubleshoot-report-excel"
                  type="checkbox"
                  name="openInExcel"
                  value="1"
                  defaultChecked={openInExcel}
                />{" "}
                Open result in Excel
              </label>
              <p className="form-hint">
                Results stay in the browser; export remains available through the report workflow.
              </p>
            </div>
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Submit
            </button>
            <a className="button button-secondary" href="/troubleshoot/reports">
              Clear
            </a>
          </div>
        </form>
      </section>
      {errorMessage ? (
        <div className="notice notice-error" role="alert">
          {errorMessage}
        </div>
      ) : null}
      {!submitted ? (
        <div className="vehicle-empty-state">
          <p>Set report filters and submit to load troubleshoot records.</p>
        </div>
      ) : errorMessage ? null : pageData?.total === 0 ? (
        <div className="vehicle-empty-state">
          <p>No records found.</p>
        </div>
      ) : (
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="troubleshoot-report-results-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">
                {pageData?.total ?? 0} result{pageData?.total === 1 ? "" : "s"}
              </p>
              <h2 id="troubleshoot-report-results-title">Report results</h2>
            </div>
          </div>
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">Troubleshoot report results</caption>
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
                {pageData?.items.map((row) => (
                  <tr key={row.id}>
                    <td>{valueOrDash(row.vehicleIdentifier)}</td>
                    <td>{valueOrDash(row.problemDescription)}</td>
                    <td>{valueOrDash(row.status)}</td>
                    <td>{dateValue(row.loggedDate)}</td>
                    <td>{valueOrDash(row.loggedBy)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination
            path="/troubleshoot/reports"
            page={pageData?.page ?? page}
            totalPages={pageData?.totalPages ?? 1}
            query={{
              submitted: 1,
              problemKeyword,
              userAccessCode: userAccessCode ?? undefined,
              fromDate,
              toDate,
              openInExcel: openInExcel ? 1 : undefined,
            }}
          />
        </section>
      )}
    </TroubleshootShell>
  );
}

export default function TroubleshootReportsPage(props: Readonly<{ searchParams: SearchParams }>) {
  return (
    <StreamedRoute>
      <TroubleshootReportsPageContent {...props} />
    </StreamedRoute>
  );
}
