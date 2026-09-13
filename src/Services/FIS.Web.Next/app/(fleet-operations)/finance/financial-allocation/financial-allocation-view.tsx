import Link from "next/link";

import {
  FinanceFrame,
  FinanceMenuLink,
  FinanceMenuSection,
} from "@/app/(fleet-operations)/finance/_components";
import {
  activateBasSegmentsAction,
  assignFundCodeAction,
  fixInvalidBasJournalAction,
  importBasAction,
} from "./actions";
import type { BasSegment, FinanceOption, FinanceRow } from "@/lib/api/finance/api-finance";

type Query = Record<string, string | string[] | undefined>;

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
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
  const target = new Set(names.map((name) => name.toLowerCase()));
  const entry = Object.entries(row).find(([key]) => target.has(key.toLowerCase()));
  if (!entry || entry[1] === null || entry[1] === undefined) return "-";
  return typeof entry[1] === "string" ? entry[1] : String(entry[1]);
}

function rowKey(row: FinanceRow, action: string) {
  return `${action}-${JSON.stringify(row)}`;
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

function BasCodeOptions({
  segments,
  placeholder,
}: Readonly<{ segments: BasSegment[]; placeholder: string }>) {
  return (
    <>
      <option value="">{placeholder}</option>
      {segments.map((segment) => (
        <option key={segment.segmentCode} value={segment.segmentNumber}>
          {segment.segmentValue || segment.segmentNumber}
        </option>
      ))}
    </>
  );
}

function InvalidBasCorrectionTable({
  rows,
  departmentCode,
  responsibilitySegments,
  objectiveSegments,
}: Readonly<{
  rows: FinanceRow[];
  departmentCode: string;
  responsibilitySegments: BasSegment[];
  objectiveSegments: BasSegment[];
}>) {
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Fix invalid BAS codes</caption>
        <thead>
          <tr>
            <th scope="col">Journal</th>
            <th scope="col">Type</th>
            <th scope="col">Responsibility</th>
            <th scope="col">Objective</th>
            <th scope="col">Previous BAS values</th>
            <th scope="col">Site</th>
            <th scope="col">Action</th>
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 ? (
            <tr>
              <td colSpan={7}>No journals with invalid BAS codes were found.</td>
            </tr>
          ) : (
            rows.map((row) => {
              const transactionId = value(row, "transactionId", "transaction_id", "id");
              const formId = `fix-bas-${transactionId}`;
              return (
                <tr key={rowKey(row, "fix-invalid-journals")}>
                  <td>{value(row, "journalNumber", "ggNumber", "gg_number")}</td>
                  <td>{value(row, "journalType", "journal_type", "reason")}</td>
                  <td>
                    <select
                      aria-label={`Responsibility BAS code for journal ${transactionId}`}
                      form={formId}
                      name="responsibility"
                      required
                    >
                      <BasCodeOptions
                        placeholder="Select responsibility"
                        segments={responsibilitySegments}
                      />
                    </select>
                  </td>
                  <td>
                    <select
                      aria-label={`Objective BAS code for journal ${transactionId}`}
                      form={formId}
                      name="objective"
                      required
                    >
                      <BasCodeOptions placeholder="Select objective" segments={objectiveSegments} />
                    </select>
                  </td>
                  <td>
                    {value(row, "responsibilityNumber", "responsibility_number")} /{" "}
                    {value(row, "objectiveNumber", "objective_number")}
                  </td>
                  <td>{value(row, "siteName", "site_name")}</td>
                  <td>
                    <form action={fixInvalidBasJournalAction} id={formId}>
                      <input name="transactionId" type="hidden" value={transactionId} />
                      <input name="departmentCode" type="hidden" value={departmentCode} />
                      <button className="button button-primary" type="submit">
                        Save
                      </button>
                    </form>
                  </td>
                </tr>
              );
            })
          )}
        </tbody>
      </table>
    </div>
  );
}

function FundCodeAllocationTable({
  rows,
  departmentCode,
  fundSegments,
}: Readonly<{
  rows: FinanceRow[];
  departmentCode: string;
  fundSegments: BasSegment[];
}>) {
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Allocate FUND codes to un-invoiced journals</caption>
        <thead>
          <tr>
            <th scope="col">Journal</th>
            <th scope="col">Vehicle</th>
            <th scope="col">Journal month</th>
            <th scope="col">FUND code</th>
            <th scope="col">Action</th>
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 ? (
            <tr>
              <td colSpan={5}>No un-invoiced journals require FUND allocation.</td>
            </tr>
          ) : (
            rows.map((row) => {
              const journalDetailCode = value(row, "journalDetailCode", "journal_detail_code");
              const vmfCode = value(row, "vmfCode", "vmf_code");
              const journalDetailTypeCode = value(
                row,
                "journalDetailTypeCode",
                "journal_detail_type_code",
              );
              const siteCode = value(row, "siteCode", "site_code");
              const journalMonth = value(row, "journalMonth", "journal_month");
              const formId = `fund-bas-${journalDetailCode}`;
              return (
                <tr key={rowKey(row, "allocate-fund-codes")}>
                  <td>{value(row, "journalNumber", "journal_number")}</td>
                  <td>{vmfCode || value(row, "registrationNumber", "vehicle")}</td>
                  <td>{journalMonth || value(row, "transactionDate")}</td>
                  <td>
                    <select
                      aria-label={`FUND code for journal ${journalDetailCode}`}
                      form={formId}
                      name="fundNumber"
                      required
                    >
                      <BasCodeOptions placeholder="Select FUND code" segments={fundSegments} />
                    </select>
                  </td>
                  <td>
                    <form action={assignFundCodeAction} id={formId}>
                      <input name="journalDetailCode" type="hidden" value={journalDetailCode} />
                      <input name="departmentCode" type="hidden" value={departmentCode} />
                      <input name="vmfCode" type="hidden" value={vmfCode} />
                      <input
                        name="journalDetailTypeCode"
                        type="hidden"
                        value={journalDetailTypeCode}
                      />
                      <input name="siteCode" type="hidden" value={siteCode} />
                      <input name="journalMonth" type="hidden" value={journalMonth} />
                      <button className="button button-primary" type="submit">
                        Save
                      </button>
                    </form>
                  </td>
                </tr>
              );
            })
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
            rows.map((row) => (
              <tr key={rowKey(row, action)}>
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

export function FinancialAllocationView({
  action,
  query,
  departments,
  segmentTypes,
  segments,
  responsibilitySegments,
  objectiveSegments,
  fundSegments,
  rows,
  resultPage,
  totalPages,
  error,
}: Readonly<{
  action: string;
  query: Query;
  departments: FinanceOption[];
  segmentTypes: FinanceOption[];
  segments: BasSegment[];
  responsibilitySegments: BasSegment[];
  objectiveSegments: BasSegment[];
  fundSegments: BasSegment[];
  rows: FinanceRow[];
  resultPage: number;
  totalPages: number;
  error: string | null;
}>) {
  return (
    <FinanceFrame title={titleFor(action)} description={descriptionFor(action)}>
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
      {action === "import-bas" ? (
        <form action={importBasAction} className="vehicle-status-maintenance-panel">
          <div className="field-grid">
            <div className="field">
              <label htmlFor="bas-import-department">Department</label>
              <select
                id="bas-import-department"
                name="departmentCode"
                defaultValue={queryValue(query, "departmentCode")}
              >
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
      ) : action === "activate-bas" ? (
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
            <SegmentTable segments={segments} action={action} />
            <div className="button-row">
              <button className="button button-primary" type="submit">
                Update List
              </button>
            </div>
          </form>
          <Paginator action={action} query={query} page={resultPage} totalPages={totalPages} />
        </>
      ) : action === "view-bas" ? (
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
                <select
                  id="bas-view-type"
                  name="segmentType"
                  defaultValue={queryValue(query, "segmentType")}
                >
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
          <SegmentTable segments={segments} action={action} />
          <Paginator action={action} query={query} page={resultPage} totalPages={totalPages} />
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
                href={`/finance/financial-allocation/${action}`}
              >
                Reset
              </Link>
            </div>
          </form>
          {action === "fix-invalid-journals" ? (
            <InvalidBasCorrectionTable
              departmentCode={queryValue(query, "departmentCode")}
              objectiveSegments={objectiveSegments}
              responsibilitySegments={responsibilitySegments}
              rows={rows}
            />
          ) : action === "allocate-fund-codes" ? (
            <FundCodeAllocationTable
              departmentCode={queryValue(query, "departmentCode")}
              fundSegments={fundSegments}
              rows={rows}
            />
          ) : (
            <GenericTable action={action} rows={rows} />
          )}
          <Paginator action={action} query={query} page={resultPage} totalPages={totalPages} />
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
