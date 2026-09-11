import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import {
  FinanceFrame,
  FinanceMenuLink,
  FinanceMenuSection,
  FinanceRestricted,
  FinanceUnavailable,
  hasFinanceRole,
} from "@/app/(fleet-operations)/finance/_components";
import { departmentOptions } from "@/app/(fleet-operations)/finance/_location-options";
import { DepartmentApiError, getDepartments } from "@/lib/api/reference-data/api-departments";
import {
  FinanceApiError,
  getBasSegmentsPage,
  getDepartmentsMissingFinancialSystemPage,
  getDepartmentsWithoutBasPage,
  getFinanceSegmentTypes,
  getInvalidBasJournalsPage,
  getUninvoicedBasJournalsPage,
  type BasSegment,
  type FinanceOption,
  type FinanceRow,
} from "@/lib/api/finance/api-finance";
import { getSession } from "@/lib/auth/session";

import { activateBasSegmentsAction, importBasAction } from "./actions";

type Query = Record<string, string | string[] | undefined>;
type AllocationPageProps = { searchParams?: Promise<Query>; action?: string };

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function positiveInteger(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function titleFor(action: string) {
  return (
    (
      {
        "import-bas": "Import BAS Segment Codes",
        "activate-bas": "Activate / De-activate BAS Segment Codes",
        "view-bas": "View BAS Segment Codes",
        "invalid-journals": "Un-Invoiced Journals with Invalid BAS Codes",
        "fix-invalid-journals": "Fix Invalid BAS Codes",
        "allocate-fund-codes": "Allocate Fund Codes to Vehicle Journals",
        "departments-no-bas": "Departments with No BAS Codes",
        "departments-missing-financial-system": "Departments with Missing Financial System",
      } as Record<string, string>
    )[action] ?? "Financial Allocation Codes"
  );
}

function descriptionFor(action: string) {
  return (
    (
      {
        "import-bas": "Import BAS segment codes for your department.",
        "activate-bas": "Activate or de-activate imported BAS codes.",
        "view-bas": "View activated, de-activated, and expired BAS codes.",
        "invalid-journals": "List un-invoiced journals with invalid BAS allocation codes.",
        "fix-invalid-journals": "Correct invalid BAS codes in un-invoiced journals.",
        "allocate-fund-codes": "Allocate BAS fund codes to vehicle journals.",
        "departments-no-bas": "List departments with no BAS codes imported.",
        "departments-missing-financial-system":
          "List departments with missing financial system settings.",
      } as Record<string, string>
    )[action] ?? "Select a finance allocation function from the menu."
  );
}

function value(row: FinanceRow, ...names: string[]) {
  const target = names.map((name) => name.toLowerCase());
  const entry = Object.entries(row).find(([key]) => target.includes(key.toLowerCase()));
  if (!entry || entry[1] === null || entry[1] === undefined) return "-";
  return typeof entry[1] === "string" ? entry[1] : String(entry[1]);
}

function pageHref(action: string, query: Query, page: number) {
  const params = new URLSearchParams({ view: "search", page: String(page) });
  for (const name of ["departmentCode", "segmentType"]) {
    const item = queryValue(query, name);
    if (item) params.set(name, item);
  }
  return `/finance/financial-allocation/${action}?${params.toString()}`;
}

function Paginator({
  action,
  query,
  page,
  totalPages,
}: Readonly<{ action: string; query: Query; page: number; totalPages: number }>) {
  if (totalPages <= 1) return null;
  return (
    <nav className="vehicle-pagination" aria-label="Financial allocation results pages">
      {page > 1 ? (
        <Link className="vehicle-pagination-button" href={pageHref(action, query, page - 1)}>
          Previous
        </Link>
      ) : (
        <span className="vehicle-pagination-button vehicle-pagination-disabled">Previous</span>
      )}
      <span>
        Page {page} of {totalPages}
      </span>
      {page < totalPages ? (
        <Link className="vehicle-pagination-button" href={pageHref(action, query, page + 1)}>
          Next
        </Link>
      ) : (
        <span className="vehicle-pagination-button vehicle-pagination-disabled">Next</span>
      )}
    </nav>
  );
}

function SegmentTable({ segments, action }: Readonly<{ segments: BasSegment[]; action: string }>) {
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">BAS segments</caption>
        <thead>
          <tr>
            <th scope="col">Segment ID</th>
            <th scope="col">Type</th>
            <th scope="col">Description</th>
            <th scope="col">Active</th>
            {action === "activate-bas" ? <th scope="col">Select</th> : null}
          </tr>
        </thead>
        <tbody>
          {segments.length === 0 ? (
            <tr>
              <td colSpan={action === "activate-bas" ? 5 : 4}>No BAS segments found.</td>
            </tr>
          ) : (
            segments.map((segment) => (
              <tr key={segment.segmentCode}>
                <td>{segment.segmentCode}</td>
                <td>{segment.segmentType || "-"}</td>
                <td>{segment.segmentValue || "-"}</td>
                <td>{segment.isActive ? "Yes" : "No"}</td>
                {action === "activate-bas" ? (
                  <td>
                    <input
                      aria-label={`Select BAS segment ${segment.segmentCode}`}
                      name="segmentCode"
                      type="checkbox"
                      value={segment.segmentCode}
                    />
                  </td>
                ) : null}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}

function GenericTable({ action, rows }: Readonly<{ action: string; rows: FinanceRow[] }>) {
  const departmentRows = action.startsWith("departments-");
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">{titleFor(action)}</caption>
        <thead>
          <tr>
            {departmentRows ? (
              <>
                <th scope="col">Department</th>
                <th scope="col">Description</th>
              </>
            ) : action === "allocate-fund-codes" ? (
              <>
                <th scope="col">Journal</th>
                <th scope="col">Vehicle</th>
                <th scope="col">Amount</th>
                <th scope="col">Transaction Date</th>
              </>
            ) : (
              <>
                <th scope="col">Journal</th>
                <th scope="col">Status / Reason</th>
                <th scope="col">Department</th>
              </>
            )}
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 ? (
            <tr>
              <td colSpan={departmentRows ? 2 : action === "allocate-fund-codes" ? 4 : 3}>
                No records found.
              </td>
            </tr>
          ) : (
            rows.map((row, index) => (
              <tr key={`${action}-${index}`}>
                {departmentRows ? (
                  <>
                    <td>{value(row, "departmentCode", "department_code")}</td>
                    <td>{value(row, "departmentName", "department_name", "description")}</td>
                  </>
                ) : action === "allocate-fund-codes" ? (
                  <>
                    <td>{value(row, "journalNumber", "journal_number")}</td>
                    <td>{value(row, "registrationNumber", "registration_number", "vehicle")}</td>
                    <td>{value(row, "amount")}</td>
                    <td>{value(row, "transactionDate", "transaction_date")}</td>
                  </>
                ) : (
                  <>
                    <td>{value(row, "journalNumber", "journal_number", "ggNumber")}</td>
                    <td>{value(row, "status", "reason", "journalType")}</td>
                    <td>{value(row, "departmentCode", "department_code")}</td>
                  </>
                )}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}

async function FinancialAllocationContent({ searchParams, action }: AllocationPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <FinanceFrame
        title="Financial Allocation Codes"
        description="Financial allocation maintenance."
      >
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasFinanceRole(session.roles))
    return (
      <FinanceFrame
        title="Financial Allocation Codes"
        description="Financial allocation maintenance."
      >
        <FinanceRestricted />
      </FinanceFrame>
    );
  const query = searchParams ? await searchParams : {};
  const normalizedAction = action?.trim().toLowerCase() ?? "";
  const validActions = [
    "import-bas",
    "activate-bas",
    "view-bas",
    "invalid-journals",
    "fix-invalid-journals",
    "allocate-fund-codes",
    "departments-no-bas",
    "departments-missing-financial-system",
  ];
  if (!validActions.includes(normalizedAction))
    return (
      <FinanceFrame
        title="Financial Allocation Codes"
        description="Financial allocation maintenance."
      >
        <div className="vehicle-menu-tiles">
          <FinanceMenuSection title="Financial Allocation Codes">
            <FinanceMenuLink href="/finance/financial-allocation/import-bas">
              Import BAS Segment Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/activate-bas">
              Activate / De-activate BAS Segment Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/view-bas">
              View BAS Segment Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/invalid-journals">
              Un-Invoiced Journals with Invalid BAS Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/fix-invalid-journals">
              Fix Invalid BAS Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/allocate-fund-codes">
              Allocate Fund Codes to Vehicle Journals
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/departments-no-bas">
              Departments with No BAS Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/departments-missing-financial-system">
              Departments with Missing Financial System
            </FinanceMenuLink>
          </FinanceMenuSection>
        </div>
      </FinanceFrame>
    );

  let departments: FinanceOption[] = [];
  let segmentTypes: FinanceOption[] = [];
  try {
    const [departmentRecords, loadedSegmentTypes] = await Promise.all([
      getDepartments(),
      getFinanceSegmentTypes(),
    ]);
    departments = departmentOptions(departmentRecords);
    segmentTypes = loadedSegmentTypes;
  } catch (error) {
    if (!(error instanceof FinanceApiError) && !(error instanceof DepartmentApiError)) throw error;
  }
  const submitted = queryValue(query, "view") === "search";
  const departmentCode = positiveInteger(queryValue(query, "departmentCode"));
  const segmentType = queryValue(query, "segmentType");
  const page = positiveInteger(queryValue(query, "page")) ?? 1;
  let segments: BasSegment[] = [];
  let rows: FinanceRow[] = [];
  let resultPage = page;
  let totalPages = 1;
  let error: string | null = null;
  if (submitted) {
    try {
      if (normalizedAction === "activate-bas" || normalizedAction === "view-bas") {
        const result = await getBasSegmentsPage(departmentCode, segmentType || undefined, page);
        segments = result.items;
        resultPage = result.page;
        totalPages = result.totalPages;
      }
      if (normalizedAction === "invalid-journals" || normalizedAction === "fix-invalid-journals") {
        const result = await getInvalidBasJournalsPage(departmentCode, page);
        rows = result.items;
        resultPage = result.page;
        totalPages = result.totalPages;
      }
      if (normalizedAction === "allocate-fund-codes") {
        const result = await getUninvoicedBasJournalsPage(departmentCode, page);
        rows = result.items;
        resultPage = result.page;
        totalPages = result.totalPages;
      }
      if (normalizedAction === "departments-no-bas") {
        const result = await getDepartmentsWithoutBasPage(page);
        rows = result.items;
        resultPage = result.page;
        totalPages = result.totalPages;
      }
      if (normalizedAction === "departments-missing-financial-system") {
        const result = await getDepartmentsMissingFinancialSystemPage(page);
        rows = result.items;
        resultPage = result.page;
        totalPages = result.totalPages;
      }
    } catch (caught) {
      error =
        caught instanceof FinanceApiError
          ? caught.message
          : "The Finance service could not be reached. Please try again.";
    }
  }
  const title = titleFor(normalizedAction);
  return (
    <FinanceFrame title={title} description={descriptionFor(normalizedAction)}>
      {queryValue(query, "message") ? (
        <div
          className={`notice ${queryValue(query, "result") === "error" || queryValue(query, "result") === "forbidden" ? "notice-error" : "notice-success"}`}
          role="status"
        >
          {queryValue(query, "message")}
        </div>
      ) : null}
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      {normalizedAction === "import-bas" ? (
        <form action={importBasAction} className="vehicle-status-maintenance-panel">
          <div className="field-grid">
            <div className="field">
              <label htmlFor="bas-import-department">Department</label>
              <select id="bas-import-department" name="departmentCode" defaultValue="">
                <option value="">Select Department</option>
                {departments.map((item) => (
                  <option key={item.value} value={item.value}>
                    {item.label}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="bas-import-start">Start Date</label>
              <input id="bas-import-start" name="startDate" type="date" />
            </div>
            <div className="field">
              <label htmlFor="bas-import-end">End Date</label>
              <input id="bas-import-end" name="endDate" type="date" />
            </div>
            <div className="field field-group-full">
              <label htmlFor="bas-import-file">Upload File</label>
              <input id="bas-import-file" name="file" type="file" accept=".csv,text/csv" required />
            </div>
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Submit
            </button>
            <Link className="button button-secondary" href="/finance">
              Finance Menu
            </Link>
          </div>
        </form>
      ) : normalizedAction === "activate-bas" ? (
        <>
          <form className="vehicle-status-maintenance-panel" method="get">
            <input name="view" type="hidden" value="search" />
            <div className="field-grid">
              <div className="field">
                <label htmlFor="bas-activate-department">Department</label>
                <select
                  id="bas-activate-department"
                  name="departmentCode"
                  defaultValue={queryValue(query, "departmentCode")}
                >
                  <option value="">All Departments</option>
                  {departments.map((item) => (
                    <option key={item.value} value={item.value}>
                      {item.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>
            <div className="button-row">
              <button className="button button-secondary" type="submit">
                Load Segments
              </button>
              <Link
                className="button button-secondary"
                href="/finance/financial-allocation/activate-bas"
              >
                Reset
              </Link>
            </div>
          </form>
          <form action={activateBasSegmentsAction} className="vehicle-status-maintenance-panel">
            <SegmentTable segments={segments} action={normalizedAction} />
            <div className="button-row">
              <button className="button button-primary" type="submit">
                Update List
              </button>
            </div>
          </form>
          <Paginator
            action={normalizedAction}
            query={query}
            page={resultPage}
            totalPages={totalPages}
          />
        </>
      ) : normalizedAction === "view-bas" ? (
        <>
          <form className="vehicle-status-maintenance-panel" method="get">
            <input name="view" type="hidden" value="search" />
            <div className="field-grid">
              <div className="field">
                <label htmlFor="bas-view-department">Department</label>
                <select
                  id="bas-view-department"
                  name="departmentCode"
                  defaultValue={queryValue(query, "departmentCode")}
                >
                  <option value="">All Departments</option>
                  {departments.map((item) => (
                    <option key={item.value} value={item.value}>
                      {item.label}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label htmlFor="bas-view-type">Segment Type</label>
                <select id="bas-view-type" name="segmentType" defaultValue={segmentType}>
                  <option value="">All Types</option>
                  {segmentTypes.map((item) => (
                    <option key={item.value} value={item.value}>
                      {item.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>
            <div className="button-row">
              <button className="button button-primary" type="submit">
                View
              </button>
              <Link
                className="button button-secondary"
                href="/finance/financial-allocation/view-bas"
              >
                Reset
              </Link>
            </div>
          </form>
          <SegmentTable segments={segments} action={normalizedAction} />
          <Paginator
            action={normalizedAction}
            query={query}
            page={resultPage}
            totalPages={totalPages}
          />
        </>
      ) : (
        <>
          <form className="vehicle-status-maintenance-panel" method="get">
            <input name="view" type="hidden" value="search" />
            <div className="field-grid">
              <div className="field">
                <label htmlFor="bas-list-department">Department</label>
                <select
                  id="bas-list-department"
                  name="departmentCode"
                  defaultValue={queryValue(query, "departmentCode")}
                >
                  <option value="">All Departments</option>
                  {departments.map((item) => (
                    <option key={item.value} value={item.value}>
                      {item.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>
            <div className="button-row">
              <button className="button button-primary" type="submit">
                Load
              </button>
              <Link
                className="button button-secondary"
                href={`/finance/financial-allocation/${normalizedAction}`}
              >
                Reset
              </Link>
            </div>
          </form>
          <GenericTable action={normalizedAction} rows={rows} />
          <Paginator
            action={normalizedAction}
            query={query}
            page={resultPage}
            totalPages={totalPages}
          />
        </>
      )}
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/finance">
          Finance Menu
        </Link>
      </div>
    </FinanceFrame>
  );
}

export function FinancialAllocationRoute(props: AllocationPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <FinancialAllocationContent {...props} />
    </Suspense>
  );
}

export default function FinancialAllocationPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  return <FinancialAllocationRoute searchParams={searchParams} />;
}
