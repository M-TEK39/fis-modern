import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import {
  AccessRestricted,
  hasReportsRole,
  ReportsFrame,
} from "@/app/(fleet-operations)/reports/_components";
import {
  AuditApiError,
  getAuditTrail,
  getPasswordHistory,
  getUserStatusHistory,
  type AuditItem,
  type AuditPagedResult,
  type PasswordHistoryItem,
  type PasswordHistoryResult,
  type UserStatusItem,
  type UserStatusResult,
} from "@/lib/api/administration/api-audit";
import { getSession } from "@/lib/auth/session";

type Query = Record<string, string | string[] | undefined>;
type Tab = "changes" | "users" | "password";

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function integer(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function dateInput(daysFromToday: number) {
  const value = new Date();
  value.setDate(value.getDate() + daysFromToday);
  return value.toISOString().slice(0, 10);
}

function dateInputMonthsFromToday(months: number) {
  const value = new Date();
  value.setMonth(value.getMonth() + months);
  return value.toISOString().slice(0, 10);
}

function formatDateTime(value: string | null) {
  if (!value) return "-";
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? value
    : parsed.toISOString().replace("T", " ").slice(0, 19);
}

function formatDate(value: string | null) {
  if (!value) return "-";
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toISOString().slice(0, 10);
}

function apiMessage(error: unknown) {
  if (error instanceof AuditApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return "The audit service is temporarily unavailable. Please try again.";
    return error.message;
  }
  return "The audit service could not be reached. Please try again.";
}

function paramsFor(values: Record<string, string | number | undefined>) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) {
    if (value !== undefined && String(value).trim() !== "") params.set(key, String(value));
  }
  return params.toString();
}

function actionClass(action: string | null) {
  switch (action?.toUpperCase()) {
    case "INSERT":
      return "vehicle-badge badge-success";
    case "UPDATE":
      return "vehicle-badge badge-warning";
    case "DELETE":
      return "vehicle-badge badge-error";
    default:
      return "vehicle-badge";
  }
}

function statusClass(status: string | null) {
  switch (status?.toLowerCase()) {
    case "enabled":
    case "unlocked":
      return "vehicle-badge badge-success";
    case "disabled":
    case "locked":
      return "vehicle-badge badge-error";
    case "password_reset":
      return "vehicle-badge badge-warning";
    default:
      return "vehicle-badge";
  }
}

function AuditPagination({
  page,
  totalPages,
  href,
}: Readonly<{ page: number; totalPages: number; href: (page: number) => string }>) {
  if (totalPages <= 1) return null;
  return (
    <nav className="vehicle-pagination" aria-label="Audit results pages">
      {page > 1 ? (
        <Link className="vehicle-pagination-button" href={href(page - 1)}>
          Previous
        </Link>
      ) : (
        <span className="vehicle-pagination-button vehicle-pagination-disabled">Previous</span>
      )}
      <span aria-current="page">
        Page {page} of {totalPages}
      </span>
      {page < totalPages ? (
        <Link className="vehicle-pagination-button" href={href(page + 1)}>
          Next
        </Link>
      ) : (
        <span className="vehicle-pagination-button vehicle-pagination-disabled">Next</span>
      )}
    </nav>
  );
}

function ChangesTab({
  query,
  result,
  error,
}: Readonly<{ query: Query; result: AuditPagedResult | null; error: string | null }>) {
  const submitted = queryValue(query, "view") === "search";
  const tableName = queryValue(query, "tableName");
  const action = queryValue(query, "action");
  const fromDate = queryValue(query, "fromDate") || dateInput(-7);
  const toDate = queryValue(query, "toDate") || dateInput(0);
  const userId = queryValue(query, "userId");
  const primaryKey = queryValue(query, "primaryKey");
  const page = integer(queryValue(query, "pageNumber")) ?? 1;
  const searchHref = (pageNumber: number) =>
    `/reports/system-audit-trail?${paramsFor({ tab: "changes", view: "search", tableName, action, fromDate, toDate, userId, primaryKey, pageNumber })}`;
  const exportHref = `/reports/system-audit-trail/export?${paramsFor({ tableName, action, fromDate, toDate, userId, primaryKey, pageNumber: page, pageSize: result?.pageSize ?? 24 })}`;

  return (
    <>
      <section className="vehicle-status-maintenance-panel">
        <form method="get">
          <input name="tab" type="hidden" value="changes" />
          <input name="view" type="hidden" value="search" />
          <input name="pageNumber" type="hidden" value="1" />
          <div className="field-grid">
            <div className="field">
              <label htmlFor="audit-table">Table Name</label>
              <input
                id="audit-table"
                name="tableName"
                placeholder="e.g. vehicle_master"
                defaultValue={tableName}
              />
            </div>
            <div className="field">
              <label htmlFor="audit-action">Action</label>
              <select id="audit-action" name="action" defaultValue={action}>
                <option value="">All</option>
                <option>INSERT</option>
                <option>UPDATE</option>
                <option>DELETE</option>
              </select>
            </div>
            <div className="field">
              <label htmlFor="audit-from">From Date</label>
              <input id="audit-from" name="fromDate" type="date" defaultValue={fromDate} />
            </div>
            <div className="field">
              <label htmlFor="audit-to">To Date</label>
              <input id="audit-to" name="toDate" type="date" defaultValue={toDate} />
            </div>
            <div className="field">
              <label htmlFor="audit-user">User Code</label>
              <input
                id="audit-user"
                name="userId"
                type="number"
                min="1"
                placeholder="user_access_code"
                defaultValue={userId}
              />
            </div>
            <div className="field">
              <label htmlFor="audit-pk">Record PK</label>
              <input
                id="audit-pk"
                name="primaryKey"
                placeholder="Primary key"
                defaultValue={primaryKey}
              />
            </div>
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Search
            </button>
            <Link
              className="button button-secondary"
              href="/reports/system-audit-trail?tab=changes"
            >
              Reset
            </Link>
            {result && result.items.length > 0 ? (
              <Link className="button button-secondary" href={exportHref}>
                Export CSV
              </Link>
            ) : null}
          </div>
        </form>
      </section>
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      <section className="vehicle-status-maintenance-panel" aria-labelledby="audit-changes-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">{result?.totalCount ?? 0} records</p>
            <h2 id="audit-changes-title">Entity Changes</h2>
          </div>
        </div>
        {!submitted ? (
          <p className="muted-copy">No records loaded. Apply filters and click Search.</p>
        ) : result?.items.length ? (
          <AuditChangesTable items={result.items} />
        ) : (
          <p className="muted-copy">No records found.</p>
        )}
        {result ? (
          <AuditPagination
            page={result.pageNumber || page}
            totalPages={result.totalPages}
            href={searchHref}
          />
        ) : null}
      </section>
    </>
  );
}

function AuditChangesTable({ items }: Readonly<{ items: AuditItem[] }>) {
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">System entity changes</caption>
        <thead>
          <tr>
            <th scope="col">Timestamp (UTC)</th>
            <th scope="col">Action</th>
            <th scope="col">Table</th>
            <th scope="col">Record PK</th>
            <th scope="col">Changed By</th>
            <th scope="col">User Code</th>
            <th scope="col">Old / New Values</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.auditId}>
              <td>{formatDateTime(item.changedAt)}</td>
              <td>
                <span className={actionClass(item.action)}>{item.action ?? "-"}</span>
              </td>
              <td>{item.tableName ?? "-"}</td>
              <td>{item.primaryKey ?? "-"}</td>
              <td>{item.actionedBy ?? "-"}</td>
              <td>{item.createdByUserCode ?? "-"}</td>
              <td>
                {item.changes ? (
                  <details>
                    <summary>View diff</summary>
                    <pre>{item.changes}</pre>
                  </details>
                ) : (
                  "-"
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function UserStatusTab({
  query,
  result,
  error,
}: Readonly<{ query: Query; result: UserStatusResult | null; error: string | null }>) {
  const submitted = queryValue(query, "view") === "search";
  const userAccessCode = queryValue(query, "userAccessCode");
  const fromDate = queryValue(query, "fromDate") || dateInputMonthsFromToday(-3);
  const toDate = queryValue(query, "toDate") || dateInput(0);
  const page = integer(queryValue(query, "pageNumber")) ?? 1;
  const searchHref = (pageNumber: number) =>
    `/reports/system-audit-trail?${paramsFor({ tab: "users", view: "search", userAccessCode, fromDate, toDate, pageNumber })}`;
  return (
    <>
      <section className="vehicle-status-maintenance-panel">
        <form method="get">
          <input name="tab" type="hidden" value="users" />
          <input name="view" type="hidden" value="search" />
          <input name="pageNumber" type="hidden" value="1" />
          <div className="field-grid">
            <div className="field">
              <label htmlFor="status-user">User Code</label>
              <input
                id="status-user"
                name="userAccessCode"
                type="number"
                min="1"
                placeholder="user_access_code"
                defaultValue={userAccessCode}
              />
            </div>
            <div className="field">
              <label htmlFor="status-from">From Date</label>
              <input id="status-from" name="fromDate" type="date" defaultValue={fromDate} />
            </div>
            <div className="field">
              <label htmlFor="status-to">To Date</label>
              <input id="status-to" name="toDate" type="date" defaultValue={toDate} />
            </div>
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Search
            </button>
            <Link className="button button-secondary" href="/reports/system-audit-trail?tab=users">
              Reset
            </Link>
          </div>
        </form>
      </section>
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      <section className="vehicle-status-maintenance-panel" aria-labelledby="user-status-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">{result?.totalCount ?? 0} records</p>
            <h2 id="user-status-title">User Enable / Disable / Lock History</h2>
          </div>
        </div>
        {!submitted ? (
          <p className="muted-copy">No records loaded. Apply filters and click Search.</p>
        ) : result?.items.length ? (
          <UserStatusTable items={result.items} />
        ) : (
          <p className="muted-copy">No records found.</p>
        )}
        {result ? (
          <AuditPagination page={page} totalPages={result.totalPages} href={searchHref} />
        ) : null}
      </section>
    </>
  );
}

function UserStatusTable({ items }: Readonly<{ items: UserStatusItem[] }>) {
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">User status history</caption>
        <thead>
          <tr>
            <th scope="col">Timestamp (UTC)</th>
            <th scope="col">User Code</th>
            <th scope="col">New Status</th>
            <th scope="col">Previous Status</th>
            <th scope="col">Changed By</th>
            <th scope="col">Reason</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.statusHistoryId}>
              <td>{formatDateTime(item.changedAt)}</td>
              <td>{item.userAccessCode}</td>
              <td>
                <span className={statusClass(item.newStatus)}>{item.newStatus ?? "-"}</span>
              </td>
              <td>{item.previousStatus ?? "-"}</td>
              <td>{item.changedByUserCode ?? "-"}</td>
              <td>{item.reason ?? "-"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function PasswordTab({
  query,
  result,
  error,
}: Readonly<{ query: Query; result: PasswordHistoryResult | null; error: string | null }>) {
  const submitted = queryValue(query, "view") === "search";
  const userAccessCode = queryValue(query, "userAccessCode");
  const page = integer(queryValue(query, "pageNumber")) ?? 1;
  const searchHref = (pageNumber: number) =>
    `/reports/system-audit-trail?${paramsFor({ tab: "password", view: "search", userAccessCode, pageNumber })}`;
  return (
    <>
      <section className="vehicle-status-maintenance-panel">
        <form method="get">
          <input name="tab" type="hidden" value="password" />
          <input name="view" type="hidden" value="search" />
          <input name="pageNumber" type="hidden" value="1" />
          <div className="field-grid">
            <div className="field">
              <label htmlFor="password-user">User Code (blank = all)</label>
              <input
                id="password-user"
                name="userAccessCode"
                type="number"
                min="1"
                placeholder="user_access_code"
                defaultValue={userAccessCode}
              />
            </div>
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Search
            </button>
            <Link
              className="button button-secondary"
              href="/reports/system-audit-trail?tab=password"
            >
              Reset
            </Link>
          </div>
        </form>
      </section>
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      <section
        className="vehicle-status-maintenance-panel"
        aria-labelledby="password-history-title"
      >
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">{result?.totalCount ?? 0} records</p>
            <h2 id="password-history-title">Password Change &amp; Expiry Status</h2>
          </div>
        </div>
        {!submitted ? (
          <p className="muted-copy">No records loaded. Apply filters and click Search.</p>
        ) : result?.items.length ? (
          <PasswordHistoryTable items={result.items} />
        ) : (
          <p className="muted-copy">No records found.</p>
        )}
        {result ? (
          <AuditPagination page={page} totalPages={result.totalPages} href={searchHref} />
        ) : null}
      </section>
    </>
  );
}

function PasswordHistoryTable({ items }: Readonly<{ items: PasswordHistoryItem[] }>) {
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Password history</caption>
        <thead>
          <tr>
            <th scope="col">User Code</th>
            <th scope="col">Last Changed (UTC)</th>
            <th scope="col">Expires On</th>
            <th scope="col">Status</th>
            <th scope="col">Changed By</th>
            <th scope="col">Failed Logins</th>
            <th scope="col">Locked Until</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={`${item.userAccessCode}-${item.lastPasswordChange}`}>
              <td>{item.userAccessCode}</td>
              <td>{formatDateTime(item.lastPasswordChange)}</td>
              <td>
                {item.passwordExpiryDate
                  ? formatDate(item.passwordExpiryDate)
                  : "Legacy (calc. 90-day)"}
              </td>
              <td>
                <span
                  className={`vehicle-badge ${item.isExpired ? "badge-error" : "badge-success"}`}
                >
                  {item.isExpired ? "EXPIRED" : "Active"}
                </span>
              </td>
              <td>{item.changedByUserCode ?? "Self / Unknown"}</td>
              <td>{item.failedLoginAttempts}</td>
              <td>{formatDateTime(item.accountLockedUntil)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

async function SystemAuditTrailPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  await connection();
  const [session, query] = await Promise.all([getSession(), searchParams]);
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <ReportsFrame
        title="System Audit Trail"
        description="The audit workspace is temporarily unavailable."
      >
        <p className="notice notice-error">
          The FIS API could not be reached. Retry when it is available.
        </p>
      </ReportsFrame>
    );
  if (!hasReportsRole(session.roles))
    return (
      <ReportsFrame
        title="System Audit Trail"
        description="Audit access is enforced on the server."
      >
        <AccessRestricted />
      </ReportsFrame>
    );

  const requestedTab = queryValue(query, "tab");
  const tab: Tab =
    requestedTab === "users" || requestedTab === "password" ? requestedTab : "changes";
  const submitted = queryValue(query, "view") === "search";
  let changes: AuditPagedResult | null = null;
  let status: UserStatusResult | null = null;
  let password: PasswordHistoryResult | null = null;
  let error: string | null = null;

  if (submitted) {
    try {
      if (tab === "changes")
        changes = await getAuditTrail({
          tableName: queryValue(query, "tableName") || undefined,
          action: queryValue(query, "action") || undefined,
          fromDate: queryValue(query, "fromDate") || undefined,
          toDate: queryValue(query, "toDate") || undefined,
          userId: integer(queryValue(query, "userId")),
          primaryKey: queryValue(query, "primaryKey") || undefined,
          pageNumber: integer(queryValue(query, "pageNumber")) ?? 1,
          pageSize: 24,
        });
      if (tab === "users")
        status = await getUserStatusHistory({
          userAccessCode: integer(queryValue(query, "userAccessCode")),
          fromDate: queryValue(query, "fromDate") || undefined,
          toDate: queryValue(query, "toDate") || undefined,
          pageNumber: integer(queryValue(query, "pageNumber")) ?? 1,
          pageSize: 24,
        });
      if (tab === "password")
        password = await getPasswordHistory({
          userAccessCode: integer(queryValue(query, "userAccessCode")),
          pageNumber: integer(queryValue(query, "pageNumber")) ?? 1,
          pageSize: 24,
        });
    } catch (caught) {
      error = apiMessage(caught);
    }
  }

  return (
    <ReportsFrame
      title="System Audit Trail"
      description="Review entity changes, account status history, and password expiry without exposing password hashes."
    >
      <div className="button-row" role="tablist" aria-label="System audit sections">
        <Link
          className={`button ${tab === "changes" ? "button-primary" : "button-secondary"}`}
          href="/reports/system-audit-trail?tab=changes"
          role="tab"
          aria-selected={tab === "changes"}
        >
          Entity Changes
        </Link>
        <Link
          className={`button ${tab === "users" ? "button-primary" : "button-secondary"}`}
          href="/reports/system-audit-trail?tab=users"
          role="tab"
          aria-selected={tab === "users"}
        >
          User Status History
        </Link>
        <Link
          className={`button ${tab === "password" ? "button-primary" : "button-secondary"}`}
          href="/reports/system-audit-trail?tab=password"
          role="tab"
          aria-selected={tab === "password"}
        >
          Password Expiry
        </Link>
      </div>
      {tab === "changes" ? (
        <ChangesTab query={query} result={changes} error={error} />
      ) : tab === "users" ? (
        <UserStatusTab query={query} result={status} error={error} />
      ) : (
        <PasswordTab query={query} result={password} error={error} />
      )}
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/reports">
          Reports Menu
        </Link>
      </div>
    </ReportsFrame>
  );
}

export default function SystemAuditTrailPage(props: Readonly<{ searchParams: Promise<Query> }>) {
  return (
    <StreamedRoute>
      <SystemAuditTrailPageContent {...props} />
    </StreamedRoute>
  );
}
