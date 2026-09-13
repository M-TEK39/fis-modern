import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import {
  FinanceFrame,
  FinanceRestricted,
  FinanceUnavailable,
} from "@/app/(fleet-operations)/finance/_components";
import {
  hasCoisIdentity,
  hasFinanceRole,
  hasFinancialReportsRole,
} from "@/app/(fleet-operations)/finance/_utils";
import { departmentOptions } from "@/app/(fleet-operations)/finance/_location-options";
import { FinanceReportTable } from "@/app/(fleet-operations)/finance/report-table";
import { getDepartments } from "@/lib/api/reference-data/api-departments";
import {
  FinanceApiError,
  getFinanceProvinces,
  getFinanceYears,
  type FinanceOption,
} from "@/lib/api/finance/api-finance";
import {
  getMissingKilometresFinanceReport,
  type FinanceReport,
} from "@/lib/api/finance/api-finance-reports";
import { getSession } from "@/lib/auth/session";

import { closeMissingKilometresAction } from "../actions";

type Query = Record<string, string | string[] | undefined>;
type Props = Readonly<{ params: Promise<{ action: string }>; searchParams: Promise<Query> }>;

const ACTIONS = [
  "fuel-consumption",
  "no-kilos-consuming-fuel",
  "kilo-gaps-pdf",
  "kilo-gaps-xls",
  "close-gaps",
] as const;
type MissingKilometresAction = (typeof ACTIONS)[number];

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function titleFor(action: string) {
  return (
    (
      {
        "fuel-consumption": "Missing Kilometres from Fuel Consumption",
        "no-kilos-consuming-fuel": "Vehicles with No Kilos but Consumed Fuel",
        "kilo-gaps-pdf":
          "Missing Kilometres Report — Kilo Gaps in the Same Department and Site (VIP Excluded)",
        "kilo-gaps-xls":
          "Missing Kilometres Report — Kilo Gaps in the Same Department and Site (VIP Excluded)",
        "close-gaps": "Automatically Capture Missing Kilometres",
      } as Record<string, string>
    )[action] ?? "Missing Kilometres"
  );
}

function descriptionFor(action: string) {
  if (action === "close-gaps")
    return "Close eligible kilometre gaps using the existing Finance workflow.";
  return "Missing-kilometres reporting with the existing Finance filters and database rules.";
}

function optionList(options: FinanceOption[], emptyLabel: string) {
  return (
    <>
      <option value="">{emptyLabel}</option>
      {options.map((item) => (
        <option key={item.value} value={item.value}>
          {item.label}
        </option>
      ))}
    </>
  );
}

function outputHref(action: "kilo-gaps-pdf" | "kilo-gaps-xls", financialYear: string) {
  const params = new URLSearchParams({
    kind: "missing-kilometres",
    action,
    financialYear,
    format: action === "kilo-gaps-xls" ? "excel" : "html",
  });
  return `/finance/reports/output?${params.toString()}`;
}

function checked(query: Query, name: string) {
  return ["1", "true", "on", "yes"].includes(queryValue(query, name).toLowerCase());
}

const MissingKilometresContent = renderMissingKilometresContent;

async function renderMissingKilometresContent({ params, searchParams }: Props) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <FinanceFrame title="Missing Kilometres" description="Missing-kilometres reporting.">
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (
    !hasFinanceRole(session.roles) ||
    !hasFinancialReportsRole(session.roles) ||
    (session.departmentCode !== "147" && !hasCoisIdentity(session.legacyUsername))
  )
    return (
      <FinanceFrame title="Missing Kilometres" description="Missing-kilometres reporting.">
        <FinanceRestricted />
      </FinanceFrame>
    );

  const { action } = await params;
  const normalizedAction = action.trim().toLowerCase();
  if (normalizedAction === "close-gaps" && !hasCoisIdentity(session.legacyUsername))
    return (
      <FinanceFrame title="Missing Kilometres" description="Missing-kilometres reporting.">
        <FinanceRestricted message="Only the legacy cois account can close kilometre gaps." />
      </FinanceFrame>
    );
  const query = await searchParams;
  const validAction = ACTIONS.includes(normalizedAction as MissingKilometresAction);
  const dateRange =
    normalizedAction === "fuel-consumption" || normalizedAction === "no-kilos-consuming-fuel";
  const financialYearAction =
    normalizedAction === "kilo-gaps-pdf" ||
    normalizedAction === "kilo-gaps-xls" ||
    normalizedAction === "close-gaps";
  let departments: ReturnType<typeof departmentOptions> = [];
  let provinces: FinanceOption[] = [];
  let years: FinanceOption[] = [];
  let error: string | null = validAction
    ? null
    : "The requested missing-kilometres action is not available.";
  try {
    const lookups: Promise<unknown>[] = [];
    if (normalizedAction === "fuel-consumption") {
      lookups.push(getDepartments(), getFinanceProvinces());
    }
    if (financialYearAction) lookups.push(getFinanceYears());
    const values = await Promise.all(lookups);
    let index = 0;
    if (normalizedAction === "fuel-consumption") {
      departments = departmentOptions(
        values[index++] as Awaited<ReturnType<typeof getDepartments>>,
      );
      provinces = values[index++] as FinanceOption[];
    }
    if (financialYearAction) years = values[index] as FinanceOption[];
  } catch (caught) {
    error =
      caught instanceof FinanceApiError
        ? caught.message
        : "Missing-kilometres lookup data could not be loaded.";
  }

  const submitted = queryValue(query, "run") === "1";
  const financialYear = queryValue(query, "financialYear");
  const reportable =
    normalizedAction === "fuel-consumption" || normalizedAction === "no-kilos-consuming-fuel";
  let report: FinanceReport | null = null;
  let output: string | null = null;
  if (submitted && validAction) {
    if (normalizedAction === "close-gaps") {
      // Mutations are submitted through the server action below.
    } else if (normalizedAction === "kilo-gaps-pdf" || normalizedAction === "kilo-gaps-xls") {
      if (!financialYear) error = "Select a financial year before generating the report.";
      else output = outputHref(normalizedAction, financialYear);
    } else if (reportable) {
      const startDate = queryValue(query, "startDate");
      const endDate = queryValue(query, "endDate");
      if (!startDate || !endDate)
        error = "Select both a start date and an end date before generating the report.";
      else {
        try {
          report = await getMissingKilometresFinanceReport({
            mode: normalizedAction,
            departmentCode: queryValue(query, "departmentCode"),
            provinceCode: queryValue(query, "provinceCode"),
            excludeUnposted: checked(query, "excludeUnposted"),
            startDate,
            endDate,
          });
        } catch (caught) {
          error =
            caught instanceof FinanceApiError
              ? caught.message
              : "The missing-kilometres report could not be generated.";
        }
      }
    }
  }

  const result = queryValue(query, "result");
  const message = queryValue(query, "message");
  const mutationError = result === "error" || result === "forbidden";
  return (
    <FinanceFrame title={titleFor(normalizedAction)} description={descriptionFor(normalizedAction)}>
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      {message ? (
        <div
          className={`notice ${mutationError ? "notice-error" : "notice-success"}`}
          role={mutationError ? "alert" : "status"}
        >
          {message}
        </div>
      ) : null}
      {normalizedAction === "close-gaps" ? (
        <form className="vehicle-status-maintenance-panel" action={closeMissingKilometresAction}>
          <div className="form-grid">
            <div className="form-field">
              <label className="form-label" htmlFor="missing-kilometres-year">
                Financial Year
              </label>
              <select
                className="form-select"
                id="missing-kilometres-year"
                name="financialYear"
                defaultValue={financialYear}
                required
              >
                {optionList(years, "Select Financial Year")}
              </select>
            </div>
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Close Kilometre Gaps
            </button>
            <Link className="button button-secondary" href="/finance">
              Finance Menu
            </Link>
          </div>
        </form>
      ) : (
        <form className="vehicle-status-maintenance-panel" method="get">
          <input name="run" type="hidden" value="1" />
          <div className="form-grid">
            {normalizedAction === "fuel-consumption" ? (
              <>
                <div className="form-field">
                  <label className="form-label" htmlFor="missing-kilometres-department">
                    Department (optional)
                  </label>
                  <select
                    className="form-select"
                    id="missing-kilometres-department"
                    name="departmentCode"
                    defaultValue={queryValue(query, "departmentCode")}
                  >
                    {optionList(departments, "Select Department")}
                  </select>
                </div>
                <div className="form-field">
                  <label className="form-label" htmlFor="missing-kilometres-province">
                    Province (optional)
                  </label>
                  <select
                    className="form-select"
                    id="missing-kilometres-province"
                    name="provinceCode"
                    defaultValue={queryValue(query, "provinceCode")}
                  >
                    {optionList(provinces, "Select Province")}
                  </select>
                </div>
              </>
            ) : null}
            {financialYearAction ? (
              <div className="form-field">
                <label className="form-label" htmlFor="missing-kilometres-year">
                  Financial Year
                </label>
                <select
                  className="form-select"
                  id="missing-kilometres-year"
                  name="financialYear"
                  defaultValue={financialYear}
                  required
                >
                  {optionList(years, "Select Financial Year")}
                </select>
              </div>
            ) : null}
            {dateRange ? (
              <>
                <div className="form-field">
                  <label className="form-label" htmlFor="missing-kilometres-start">
                    Start Date
                  </label>
                  <input
                    className="form-input"
                    id="missing-kilometres-start"
                    name="startDate"
                    type="date"
                    defaultValue={queryValue(query, "startDate")}
                    required
                  />
                </div>
                <div className="form-field">
                  <label className="form-label" htmlFor="missing-kilometres-end">
                    End Date
                  </label>
                  <input
                    className="form-input"
                    id="missing-kilometres-end"
                    name="endDate"
                    type="date"
                    defaultValue={queryValue(query, "endDate")}
                    required
                  />
                </div>
              </>
            ) : null}
            {normalizedAction === "fuel-consumption" ? (
              <div className="form-field">
                <label className="form-checkbox" htmlFor="missing-kilometres-exclude">
                  <input
                    id="missing-kilometres-exclude"
                    name="excludeUnposted"
                    type="checkbox"
                    value="1"
                    defaultChecked={checked(query, "excludeUnposted")}
                  />{" "}
                  Exclude Unposted
                </label>
              </div>
            ) : null}
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              {financialYearAction ? "Prepare Report" : "Generate Report"}
            </button>
            <Link className="button button-secondary" href="/finance">
              Finance Menu
            </Link>
          </div>
        </form>
      )}
      {output ? (
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="missing-kilometres-output"
        >
          <h2 id="missing-kilometres-output">Report ready</h2>
          <p className="muted-copy">The report is generated on the authenticated server path.</p>
          <a className="button button-primary" href={output} target="_blank" rel="noreferrer">
            {normalizedAction === "kilo-gaps-xls"
              ? "Download Excel report"
              : "Open printable report"}
          </a>
        </section>
      ) : null}
      {report ? (
        <FinanceReportTable
          report={report}
          basePath={`/finance/missing-kilometres/${normalizedAction}`}
          query={query}
          page={Number(queryValue(query, "page")) || 1}
          printOrientation="portrait"
        />
      ) : null}
    </FinanceFrame>
  );
}

export default function MissingKilometresPage(props: Props) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <MissingKilometresContent {...props} />
    </Suspense>
  );
}
