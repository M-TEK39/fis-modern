import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import {
  FinanceFrame,
  FinanceRestricted,
  FinanceUnavailable,
  hasFinanceRole,
} from "@/app/(fleet-operations)/finance/_components";
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
  for (const name of ["departmentCode", "siteCode", "vmfCode", "startDate", "endDate"]) {
    const value = queryValue(query, name);
    if (value) params.set(name, value);
  }
  return `/finance/reports/output?${params.toString()}`;
}

export default async function FinanceAuditTrailModePage({ params, searchParams }: AuditPageProps) {
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
  if (!hasFinanceRole(session.roles))
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
  let departments: FinanceOption[] = [];
  let sites: FinanceOption[] = [];
  let lookupError: string | null = null;
  try {
    const [departmentRecords, siteRecords] = await Promise.all([getDepartments(), getSites()]);
    departments = departmentOptions(departmentRecords);
    sites = siteOptions(siteRecords);
  } catch (error) {
    if (
      error instanceof FinanceApiError ||
      error instanceof DepartmentApiError ||
      error instanceof SiteApiError
    )
      lookupError = error.message;
    else throw error;
  }
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
    else if (mode !== "vehicle" && !positiveInteger(queryValue(query, "departmentCode")))
      error = "Select a department before running the audit trail report.";
    else if (mode === "site" && !positiveInteger(queryValue(query, "siteCode")))
      error = "Select a site before running the audit trail report.";
    else if (mode === "vehicle" && !positiveInteger(queryValue(query, "vmfCode")))
      error = "Enter a vehicle VMF code before running the audit trail report.";
    else
      output = {
        href: outputHref(mode, reportAction, outputFormat, query),
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
              <select
                className="form-select"
                id="audit-department"
                name="departmentCode"
                defaultValue={queryValue(query, "departmentCode")}
                required
              >
                {optionList(departments, "Select Department")}
              </select>
            </div>
          ) : (
            <div className="form-field">
              <label className="form-label" htmlFor="audit-vmf">
                Vehicle VMF Code
              </label>
              <input
                className="form-input"
                id="audit-vmf"
                name="vmfCode"
                defaultValue={queryValue(query, "vmfCode")}
                inputMode="numeric"
                required
              />
            </div>
          )}
          {mode === "site" ? (
            <div className="form-field">
              <label className="form-label" htmlFor="audit-site">
                Site
              </label>
              <select
                className="form-select"
                id="audit-site"
                name="siteCode"
                defaultValue={queryValue(query, "siteCode")}
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
