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
import { hasAuditTrailReportsAccess, hasRole } from "@/app/(fleet-operations)/finance/_utils";
import { departmentOptions, siteOptions } from "@/app/(fleet-operations)/finance/_location-options";
import { DepartmentApiError, getDepartments } from "@/lib/api/reference-data/api-departments";
import { FinanceApiError, type FinanceOption } from "@/lib/api/finance/api-finance";
import { SiteApiError, getSites } from "@/lib/api/reference-data/api-sites";
import { getSession } from "@/lib/auth/session";

type Query = Record<string, string | string[] | undefined>;
type AuditPageProps = Readonly<{ params: Promise<{ mode: string }>; searchParams: Promise<Query> }>;

const AUDIT_TYPES = [
  ["els", "Show ELS Audit Trail"],
  ["manual-kilos", "Show Manual Kilo's Audit Trail"],
  ["contracts", "Show Vehicle Contracts Audit Trail"],
  ["vip-taxi", "Show VIP and TAXI Audit Trail"],
] as const;

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function positiveInteger(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function titleFor(mode: string) {
  return mode === "site"
    ? "Audit Trail grouped by Site"
    : mode === "vehicle"
      ? "Audit Trail grouped per Vehicle"
      : "Audit Trail grouped by Department";
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

function outputHref(
  mode: string,
  reportAction: string,
  outputFormat: "pdf" | "excel",
  query: Query,
) {
  const params = new URLSearchParams({
    kind: "audit",
    action: mode,
    reportAction,
    outputFormat,
    format: outputFormat === "excel" ? "excel" : "html",
  });
  for (const name of [
    "departmentCode",
    "siteCode",
    "vmfCode",
    "vehicleNumber",
    "numberType",
    "startDate",
    "endDate",
  ]) {
    const value = queryValue(query, name);
    if (value) params.set(name, value);
  }
  return `/finance/reports/output?${params.toString()}`;
}

const FinanceAuditTrailModeContent = renderFinanceAuditTrailModeContent;

async function renderFinanceAuditTrailModeContent({ params, searchParams }: AuditPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  const { mode: rawMode } = await params;
  const mode = rawMode.trim().toLowerCase();
  if (session.status !== "authenticated")
    return (
      <FinanceFrame title={titleFor(mode)} description="Audit trail parameters.">
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasAuditTrailReportsAccess(session.roles))
    return (
      <FinanceFrame title={titleFor(mode)} description="Audit trail parameters.">
        <FinanceRestricted />
      </FinanceFrame>
    );
  if (!["department", "site", "vehicle"].includes(mode))
    return (
      <FinanceFrame title="Audit Trail Reports" description="Audit trail reporting.">
        <FinanceRestricted message="The requested audit trail mode is not available." />
      </FinanceFrame>
    );

  const query = await searchParams;
  const profileDepartmentCode = positiveInteger(session.departmentCode ?? "");
  const profileSiteCode = positiveInteger(session.siteCode ?? "");
  const canSelectAllDepartments =
    hasRole(session.roles, "Administrator") ||
    hasRole(session.roles, "Admin") ||
    hasRole(session.roles, "Financial Data (All Departments)");
  let departments: FinanceOption[] = [];
  let sites: FinanceOption[] = [];
  let lookupError: string | null = null;
  try {
    const [departmentRecords, siteRecords] = await Promise.all([getDepartments(), getSites()]);
    departments = departmentOptions(
      canSelectAllDepartments
        ? departmentRecords
        : departmentRecords.filter(
            (department) => department.departmentCode === profileDepartmentCode,
          ),
    );
    const selectedDepartmentCode =
      positiveInteger(queryValue(query, "departmentCode")) ?? profileDepartmentCode;
    sites = siteOptions(
      siteRecords.filter((site) => site.departmentCode === selectedDepartmentCode),
    );
  } catch (error) {
    if (
      error instanceof FinanceApiError ||
      error instanceof DepartmentApiError ||
      error instanceof SiteApiError
    )
      lookupError = error.message;
    else throw error;
  }
  const selectedDepartmentCode =
    queryValue(query, "departmentCode") ||
    (profileDepartmentCode ? String(profileDepartmentCode) : "");
  const selectedSiteCode =
    queryValue(query, "siteCode") || (profileSiteCode ? String(profileSiteCode) : "");
  const selectedDepartment = departments.find((item) => item.value === selectedDepartmentCode);
  const outputQuery = {
    ...query,
    ...(mode !== "vehicle" && selectedDepartmentCode
      ? { departmentCode: selectedDepartmentCode }
      : {}),
    ...(mode === "site" && selectedSiteCode ? { siteCode: selectedSiteCode } : {}),
  };
  const submitted = queryValue(query, "run") === "1";
  const rawReportAction = queryValue(query, "reportAction");
  const outputFormat = rawReportAction.endsWith("-excel") ? "excel" : "pdf";
  const reportAction = rawReportAction.replace(/-excel$/, "");
  let error = lookupError;
  let output: { href: string; label: string } | null = null;
  if (submitted) {
    if (!AUDIT_TYPES.some(([value]) => value === reportAction))
      error = "Select an audit trail report before continuing.";
    else if (!queryValue(query, "startDate") || !queryValue(query, "endDate"))
      error = "Select a start and end date before running the audit trail report.";
    else if (
      mode !== "vehicle" &&
      !positiveInteger(queryValue(query, "departmentCode") || String(profileDepartmentCode ?? ""))
    )
      error = "Select a department before running the audit trail report.";
    else if (
      mode === "site" &&
      !positiveInteger(queryValue(query, "siteCode") || String(profileSiteCode ?? ""))
    )
      error = "Select a site before running the audit trail report.";
    else if (mode === "vehicle" && !queryValue(query, "vehicleNumber"))
      error = "Enter a GG or GP vehicle number before running the audit trail report.";
    else
      output = {
        href: outputHref(mode, reportAction, outputFormat, outputQuery),
        label: outputFormat === "excel" ? "Download Excel report" : "Open printable report",
      };
  }

  return (
    <FinanceFrame title={titleFor(mode)} description="Audit trail parameters.">
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      <form className="vehicle-status-maintenance-panel" method="get">
        <input name="run" type="hidden" value="1" />
        <div className="form-grid">
          {mode !== "vehicle" ? (
            <div className="form-field">
              <label className="form-label" htmlFor="audit-department">
                Department
              </label>
              {canSelectAllDepartments ? (
                <select
                  className="form-select"
                  id="audit-department"
                  name="departmentCode"
                  defaultValue={selectedDepartmentCode}
                  required
                >
                  {optionList(departments, "Select Department")}
                </select>
              ) : profileDepartmentCode ? (
                <>
                  <input name="departmentCode" type="hidden" value={profileDepartmentCode} />
                  <div className="form-readonly-value">
                    {selectedDepartment?.label ?? `Department (${profileDepartmentCode})`}
                  </div>
                </>
              ) : (
                <div className="form-readonly-value">No report department is available.</div>
              )}
            </div>
          ) : (
            <div className="form-field">
              <label className="form-label" htmlFor="audit-vehicle-number">
                GG / GP Number
              </label>
              <input
                className="form-input"
                id="audit-vehicle-number"
                name="vehicleNumber"
                defaultValue={queryValue(query, "vehicleNumber")}
                required
              />
            </div>
          )}
          {mode === "vehicle" ? (
            <div className="form-field">
              <label className="form-label" htmlFor="audit-number-type">
                Number Type
              </label>
              <select
                className="form-select"
                id="audit-number-type"
                name="numberType"
                defaultValue={queryValue(query, "numberType") || "gp"}
              >
                <option value="gp">GP Number</option>
                <option value="gg">GG Number</option>
              </select>
            </div>
          ) : null}
          {mode === "site" ? (
            <div className="form-field">
              <label className="form-label" htmlFor="audit-site">
                Site
              </label>
              <select
                className="form-select"
                id="audit-site"
                name="siteCode"
                defaultValue={selectedSiteCode}
                required
              >
                {optionList(sites, "Select Site")}
              </select>
            </div>
          ) : null}
          <div className="form-field">
            <label className="form-label" htmlFor="audit-from">
              Start Date
            </label>
            <input
              className="form-input"
              id="audit-from"
              name="startDate"
              type="date"
              defaultValue={queryValue(query, "startDate")}
              required
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="audit-to">
              End Date
            </label>
            <input
              className="form-input"
              id="audit-to"
              name="endDate"
              type="date"
              defaultValue={queryValue(query, "endDate")}
              required
            />
          </div>
        </div>
        <div className="form-section">
          <h2 className="form-section-title">Audit Trail PDF Reports</h2>
          <div className="button-row">
            {AUDIT_TYPES.map(([value, label]) => (
              <button
                className="button button-secondary"
                key={`${value}-pdf`}
                name="reportAction"
                type="submit"
                value={value}
              >
                {label}
              </button>
            ))}
          </div>
        </div>
        <div className="form-section">
          <h2 className="form-section-title">Audit Trail Excel Reports</h2>
          <div className="button-row">
            {AUDIT_TYPES.map(([value, label]) => (
              <button
                className="button button-secondary"
                key={`${value}-excel`}
                name="reportAction"
                type="submit"
                value={`${value}-excel`}
              >
                {label.replace("Show ", "Download ")}
              </button>
            ))}
          </div>
        </div>
        <div className="button-row">
          <Link className="button button-secondary" href="/finance/audit-trail">
            Back
          </Link>
        </div>
      </form>
      {output ? (
        <section className="vehicle-status-maintenance-panel" aria-labelledby="audit-output">
          <h2 id="audit-output">Report ready</h2>
          <a className="button button-primary" href={output.href} target="_blank" rel="noreferrer">
            {output.label}
          </a>
        </section>
      ) : null}
    </FinanceFrame>
  );
}

export default function FinanceAuditTrailModePage(props: AuditPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <FinanceAuditTrailModeContent {...props} />
    </Suspense>
  );
}
