import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import {
  createLogbookAction,
  updateLogbookAction,
} from "@/app/(fleet-operations)/log-books/actions";
import {
  LogbookShell,
  LogbookTable,
  VehicleSearchForm,
} from "@/app/(fleet-operations)/log-books/_components";
import {
  accessRestricted,
  getLogbookSession,
  hasLogbookAccess,
  parsePositiveInteger,
  queryValue,
  sessionMessage,
} from "@/app/(fleet-operations)/log-books/_page";
import {
  DEFAULT_LOGBOOK_PAGE_SIZE,
  getLogbookPage,
  getLogbookVehicleOptions,
  LogbookApiError,
  type LogbookVehicleOption,
  type LogbookRecord,
} from "@/lib/api/fleet-operations/api-logbooks";
import { getSites, SiteApiError } from "@/lib/api/reference-data/api-sites";

const routePath = "/log-books/maintenance";

function historyPath(search: string, mode: string, vmfCode: number | null, page: number) {
  const params = new URLSearchParams({ mode });
  if (search) params.set("search", search);
  if (vmfCode) params.set("vmfCode", String(vmfCode));
  if (page > 1) params.set("page", String(page));
  return `${routePath}?${params.toString()}`;
}

function LogbookMaintenancePagination({
  search,
  mode,
  vmfCode,
  page,
  totalPages,
}: Readonly<{
  search: string;
  mode: string;
  vmfCode: number;
  page: number;
  totalPages: number;
}>) {
  if (totalPages <= 1) return null;

  return (
    <nav className="vehicle-pagination" aria-label="Vehicle logbook handout pages">
      {page > 1 ? (
        <Link
          className="vehicle-pagination-button"
          href={historyPath(search, mode, vmfCode, page - 1)}
          aria-label={`Go to logbook handout page ${page - 1}`}
        >
          Previous
        </Link>
      ) : (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Previous
        </span>
      )}
      <span className="vehicle-pagination-meta" aria-live="polite">
        Page {page} of {totalPages}
      </span>
      {page < totalPages ? (
        <Link
          className="vehicle-pagination-button"
          href={historyPath(search, mode, vmfCode, page + 1)}
          aria-label={`Go to logbook handout page ${page + 1}`}
        >
          Next
        </Link>
      ) : (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Next
        </span>
      )}
    </nav>
  );
}

function filterVehicles(options: readonly LogbookVehicleOption[], search: string, mode: string) {
  const normalized = search.trim().toLocaleLowerCase();
  if (!normalized) return [];
  const isGp = mode.toLocaleUpperCase() === "GP";
  return options
    .filter((vehicle) =>
      (isGp ? vehicle.registrationNumber : vehicle.fleetNumber)
        ?.toLocaleLowerCase()
        .includes(normalized),
    )
    .sort((left, right) => {
      const leftValue = isGp ? left.registrationNumber : left.fleetNumber;
      const rightValue = isGp ? right.registrationNumber : right.fleetNumber;
      const leftExact = leftValue?.trim().toLocaleLowerCase() === normalized ? 0 : 1;
      const rightExact = rightValue?.trim().toLocaleLowerCase() === normalized ? 0 : 1;
      return leftExact - rightExact || left.vmfCode - right.vmfCode;
    });
}

function statusMessage(query: Record<string, string | string[] | undefined>) {
  const key = ["saved", "updated", "deleted", "error"].find((name) => query[name]);
  return key ? { key, value: queryValue(query[key]) } : null;
}

function LogbookForm({
  record,
  vmfCode,
  sites,
  returnPath,
}: Readonly<{
  record: LogbookRecord | null;
  vmfCode: number;
  sites: readonly { siteCode: number; description: string | null }[];
  returnPath: string;
}>) {
  const action = record ? updateLogbookAction : createLogbookAction;
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="logbook-form-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{record ? `Logbook #${record.logbookCode}` : "New handout"}</p>
          <h2 id="logbook-form-title">{record ? "Edit logbook handout" : "Add logbook handout"}</h2>
        </div>
      </div>
      <form id="logbook-form" action={action}>
        <input name="returnPath" type="hidden" value={returnPath} />
        {record ? <input name="logbookId" type="hidden" value={record.logbookCode} /> : null}
        <input name="vmfCode" type="hidden" value={vmfCode} />
        {record?.dateCreated ? (
          <input name="dateCreated" type="hidden" value={record.dateCreated.slice(0, 10)} />
        ) : null}
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="logbook-handout-date">
              Handout date
            </label>
            <input
              className="form-input"
              id="logbook-handout-date"
              name="handoutDate"
              type="date"
              defaultValue={record?.handoutDate?.slice(0, 10) ?? ""}
              required
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="logbook-begin-number">
              Begin number
            </label>
            <input
              className="form-input"
              id="logbook-begin-number"
              name="beginNumber"
              maxLength={8}
              defaultValue={record?.beginNumber ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="logbook-end-number">
              End number
            </label>
            <input
              className="form-input"
              id="logbook-end-number"
              name="endNumber"
              maxLength={8}
              defaultValue={record?.endNumber ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="logbook-site">
              Site
            </label>
            <select
              className="form-select"
              id="logbook-site"
              name="siteCode"
              defaultValue={record?.siteCode?.toString() ?? ""}
            >
              <option value="">Select site</option>
              {sites.map((site) => (
                <option key={site.siteCode} value={site.siteCode}>
                  {site.description || "Unnamed site"} ({site.siteCode})
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="logbook-receiver">
              Receiver name
            </label>
            <input
              className="form-input"
              id="logbook-receiver"
              name="receiverName"
              maxLength={25}
              defaultValue={record?.receiverName ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="logbook-telephone">
              Receiver telephone
            </label>
            <input
              className="form-input"
              id="logbook-telephone"
              name="telephoneNumber"
              maxLength={20}
              defaultValue={record?.telephoneNumber ?? ""}
            />
          </div>
          <div className="form-field form-group-full">
            <label className="form-label" htmlFor="logbook-comment">
              Comment
            </label>
            <textarea
              className="form-input"
              id="logbook-comment"
              name="comment"
              maxLength={60}
              rows={3}
              defaultValue={record?.comment ?? ""}
            />
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            {record ? "Save changes" : "Add handout"}
          </button>
          <Link className="button button-secondary" href={returnPath}>
            Cancel
          </Link>
        </div>
      </form>
    </section>
  );
}

const LogbookMaintenancePageContent = renderLogbookMaintenancePageContent;

async function renderLogbookMaintenancePageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLogbookSession();
  const problem = sessionMessage(session, "/log-books/maintenance");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLogbookAccess(session))
    return accessRestricted("Your profile does not include Logbooks access.");

  const query = await searchParams;
  const search = queryValue(query.search);
  const mode = queryValue(query.mode) === "GP" ? "GP" : "GG";
  const vmfCode = parsePositiveInteger(queryValue(query.vmfCode));
  const editCode = parsePositiveInteger(queryValue(query.edit));
  const requestedPage = parsePositiveInteger(queryValue(query.page)) ?? 1;
  const retryPath = historyPath(search, mode, vmfCode, requestedPage);
  const message = statusMessage(query);

  try {
    const [options, sites, logbookPage] = await Promise.all([
      getLogbookVehicleOptions(),
      getSites(),
      vmfCode
        ? getLogbookPage({
            page: requestedPage,
            pageSize: DEFAULT_LOGBOOK_PAGE_SIZE,
            vmfCode,
          })
        : Promise.resolve(null),
    ]);
    const matches = filterVehicles(options, search, mode);
    const currentPage = logbookPage?.page ?? requestedPage;
    const basePath = historyPath(search, mode, vmfCode, currentPage);
    const selectedRecords = logbookPage?.items ?? [];
    const editing = editCode
      ? (selectedRecords.find((record) => record.logbookCode === editCode) ?? null)
      : null;
    const showForm = query.form === "new" || editing !== null;
    return (
      <LogbookShell
        title="Logbook Maintenance"
        description="Search a vehicle, review its handouts, and add or edit a logbook."
      >
        {message ? (
          <p
            className={`alert ${message.key === "error" ? "alert-error" : "alert-success"}`}
            role="status"
          >
            {message.value}
          </p>
        ) : null}
        <VehicleSearchForm
          action="/log-books/maintenance"
          search={search}
          mode={mode}
          vmfCode={vmfCode?.toString() ?? ""}
          options={matches}
        />
        <div className="button-row">
          {vmfCode ? (
            <Link className="button button-primary" href={`${basePath}&form=new`}>
              Add handout for selected vehicle
            </Link>
          ) : null}
          <Link className="button button-secondary" href="/log-books">
            Main menu
          </Link>
        </div>
        {vmfCode ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="logbook-maintenance-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {logbookPage?.total ?? 0} record{logbookPage?.total === 1 ? "" : "s"}
                </p>
                <h2 id="logbook-maintenance-results-title">Vehicle logbook handouts</h2>
              </div>
            </div>
            <LogbookTable records={selectedRecords} mode="maintenance" returnPath={basePath} />
            {logbookPage ? (
              <>
                <LogbookMaintenancePagination
                  search={search}
                  mode={mode}
                  vmfCode={vmfCode}
                  page={logbookPage.page}
                  totalPages={logbookPage.totalPages}
                />
                <div className="pagination-meta">
                  Total records: {logbookPage.total} | Page size: {logbookPage.pageSize}
                </div>
              </>
            ) : null}
          </section>
        ) : (
          <p className="muted-copy">
            Search for a vehicle, then select it to load its logbook handouts.
          </p>
        )}
        {showForm && vmfCode ? (
          <LogbookForm record={editing} vmfCode={vmfCode} sites={sites} returnPath={basePath} />
        ) : null}
      </LogbookShell>
    );
  } catch (error) {
    const messageText =
      error instanceof LogbookApiError ||
      error instanceof SiteApiError
        ? "The Logbooks service is temporarily unavailable. Please try again."
        : "Logbook maintenance could not be loaded.";
    return (
      <LogbookShell
        title="Logbook Maintenance"
        description="Search a vehicle, review its handouts, and add or edit a logbook."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>{messageText}</h2>
          <Link className="button button-secondary" href={retryPath}>
            Try again
          </Link>
        </section>
      </LogbookShell>
    );
  }
}

export default function LogbookMaintenancePage(
  props: Parameters<typeof LogbookMaintenancePageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LogbookMaintenancePageContent {...props} />
    </Suspense>
  );
}
