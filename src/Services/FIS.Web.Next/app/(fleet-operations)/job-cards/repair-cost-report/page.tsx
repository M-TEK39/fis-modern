import Link from "next/link";

import {
  JobCardPagination,
  RepairCostTable,
  hasJobCardAccess,
  hasRole,
  formatMoney,
} from "@/app/(fleet-operations)/job-cards/_components";
import {
  accessRestricted,
  getJobCardSession,
  JobCardPageBoundary,
  jobCardPageHref,
  queryPage,
  queryValue,
  sessionMessage,
} from "@/app/(fleet-operations)/job-cards/_page";
import {
  DEFAULT_JOB_CARD_PAGE_SIZE,
  getRepairCostReportPage,
  JobCardApiError,
} from "@/lib/api/fleet-operations/api-job-cards";
import { getSites } from "@/lib/api/reference-data/api-sites";

function positiveNumber(value: string) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function validDate(value: string) {
  return /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : undefined;
}

export default function RepairCostReportPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <JobCardPageBoundary>
      <RepairCostReportContent searchParams={searchParams} />
    </JobCardPageBoundary>
  );
}

async function RepairCostReportContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  const problem = sessionMessage(session, "/job-cards/repair-cost-report");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasRole(session.roles, "capturer") && !hasJobCardAccess(session.accessLevel, session.roles))
    return accessRestricted("Your profile does not include Job Card access.");
  const query = await searchParams;
  const vmfCode = positiveNumber(queryValue(query.vmfCode));
  const siteCode = positiveNumber(queryValue(query.siteCode));
  const fromDate = validDate(queryValue(query.fromDate));
  const toDate = validDate(queryValue(query.toDate));
  const page = queryPage(query.page);
  try {
    const [report, sites] = await Promise.all([
      getRepairCostReportPage({
        vmfCode,
        siteCode,
        fromDate,
        toDate,
        page,
        pageSize: DEFAULT_JOB_CARD_PAGE_SIZE,
      }),
      getSites().catch(() => null),
    ]);
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="repair-cost-report-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Job Cards</p>
              <h1 id="repair-cost-report-title">Repair Cost Report</h1>
              <p>Review captured repair costs for completed job cards.</p>
            </div>
            <Link className="button button-secondary" href="/job-cards/capturer-default">
              Main menu
            </Link>
          </header>
          <form className="vehicle-status-maintenance-panel" method="get">
            <input type="hidden" name="page" value="1" />
            <div className="form-grid">
              <div className="form-field">
                <label className="form-label" htmlFor="repair-report-vmf">
                  VMF code
                </label>
                <input
                  className="form-input"
                  id="repair-report-vmf"
                  name="vmfCode"
                  type="number"
                  min="1"
                  defaultValue={vmfCode ?? ""}
                />
              </div>
              <div className="form-field">
                <label className="form-label" htmlFor="repair-report-site">
                  Site
                </label>
                {sites ? (
                  <select
                    className="form-select"
                    id="repair-report-site"
                    name="siteCode"
                    defaultValue={siteCode ?? ""}
                  >
                    <option value="">All sites</option>
                    {sites.map((site) => (
                      <option key={site.siteCode} value={site.siteCode}>
                        {site.description || `Site ${site.siteCode}`} ({site.siteCode})
                      </option>
                    ))}
                  </select>
                ) : (
                  <p className="muted-copy" role="status">
                    Site options are temporarily unavailable. Refresh to filter by site.
                  </p>
                )}
              </div>
              <div className="form-field">
                <label className="form-label" htmlFor="repair-report-from">
                  From date
                </label>
                <input
                  className="form-input"
                  id="repair-report-from"
                  name="fromDate"
                  type="date"
                  defaultValue={fromDate ?? ""}
                />
              </div>
              <div className="form-field">
                <label className="form-label" htmlFor="repair-report-to">
                  To date
                </label>
                <input
                  className="form-input"
                  id="repair-report-to"
                  name="toDate"
                  type="date"
                  defaultValue={toDate ?? ""}
                />
              </div>
            </div>
            <div className="button-row">
              <button className="button button-primary" type="submit">
                Run report
              </button>
              <Link className="button button-secondary" href="/job-cards/repair-cost-report">
                Clear
              </Link>
            </div>
          </form>
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="repair-report-summary-title"
          >
            <p className="eyebrow">
              {report.totalRecords} record{report.totalRecords === 1 ? "" : "s"}
            </p>
            <h2 id="repair-report-summary-title">Totals</h2>
            <div className="form-grid">
              <p>
                <strong>Grand total</strong>
                <br />
                {formatMoney(report.grandTotal)}
              </p>
              <p>
                <strong>Labour</strong>
                <br />
                {formatMoney(report.totalLabour)}
              </p>
              <p>
                <strong>Parts</strong>
                <br />
                {formatMoney(report.totalParts)}
              </p>
              <p>
                <strong>Other</strong>
                <br />
                {formatMoney(report.totalOther)}
              </p>
            </div>
            <RepairCostTable lines={report.lineItems} />
            <JobCardPagination
              page={report.page}
              totalPages={report.totalPages}
              pageHref={(nextPage) =>
                jobCardPageHref("/job-cards/repair-cost-report", query, nextPage)
              }
            />
          </section>
        </section>
      </main>
    );
  } catch (error) {
    const message =
      error instanceof JobCardApiError && error.reason === "unavailable"
        ? "The Job Cards service is temporarily unavailable. Please try again."
        : "The repair cost report could not be loaded.";
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>{message}</h2>
          <Link className="button button-secondary" href="/job-cards/repair-cost-report">
            Try again
          </Link>
        </section>
      </main>
    );
  }
}
