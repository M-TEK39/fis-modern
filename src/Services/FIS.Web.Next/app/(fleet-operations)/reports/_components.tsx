import Link from "next/link";
import type { ReactNode } from "react";

import type { LegacyReport } from "@/lib/api/reports/api-legacy-reports";
import { legacyReportHref, queryValue } from "./_utils";
import type { ReportField, ReportMenuEntry, ReportQuery } from "./_utils";

export function ReportsFrame({
  title,
  description,
  children,
  backHref = "/reports",
  backLabel = "Reports Menu",
}: Readonly<{
  title: string;
  description: string;
  children: ReactNode;
  backHref?: string;
  backLabel?: string;
}>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="reports-page-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Fleet reports</p>
            <h1 id="reports-page-title">{title}</h1>
            <p>{description}</p>
          </div>
          <Link className="button button-secondary" href={backHref}>
            {backLabel}
          </Link>
        </header>
        {children}
      </section>
    </main>
  );
}

export function AccessRestricted({
  message = "Your account needs the legacy Reports permission.",
}: Readonly<{ message?: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access reports.</h2>
      <p className="muted-copy">{message}</p>
    </section>
  );
}

export function ReportsUnavailable({
  message = "The FIS API could not be reached. Retry when it is available.",
}: Readonly<{ message?: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>The report could not be loaded.</h2>
      <p className="muted-copy">{message}</p>
    </section>
  );
}

export function ReportMenu({
  entries,
  slug,
  children,
}: Readonly<{
  entries: readonly ReportMenuEntry[];
  slug: string;
  children?: ReactNode;
}>) {
  return (
    <div className="vehicle-menu-tiles">
      <section className="vehicle-menu-tile">
        <h2 className="vehicle-menu-header">Report Menu</h2>
        <div className="vehicle-menu-body">
          {entries.map((entry) => (
            <div
              className="vehicle-menu-item"
              key={`${entry.label}-${entry.key ?? entry.href ?? "entry"}`}
            >
              {entry.href ? (
                <Link className="vehicle-menu-link" href={entry.href}>
                  {entry.label}
                </Link>
              ) : (
                <Link className="vehicle-menu-link" href={legacyReportHref(slug, entry.key)}>
                  {entry.label}
                </Link>
              )}
              {entry.badge ? <span className="vehicle-menu-badge">{entry.badge}</span> : null}
            </div>
          ))}
          {children}
        </div>
      </section>
    </div>
  );
}

export function ReportFilterForm({
  slug,
  reportKey,
  fields,
  query,
}: Readonly<{
  slug: string;
  reportKey?: string;
  fields: readonly ReportField[];
  query: ReportQuery;
}>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <input name="view" type="hidden" value="report" />
      {reportKey ? <input name="rtype" type="hidden" value={reportKey} /> : null}
      <div className="form-grid">
        {fields.map((field) => (
          <div className="form-field" key={field.name}>
            <label className="form-label" htmlFor={`report-${slug}-${field.name}`}>
              {field.label}
            </label>
            {field.options ? (
              <select
                className="form-select"
                id={`report-${slug}-${field.name}`}
                name={field.name}
                defaultValue={queryValue(query, field.name)}
              >
                {field.options.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            ) : (
              <input
                className="form-input"
                id={`report-${slug}-${field.name}`}
                name={field.name}
                type={field.type ?? "text"}
                defaultValue={queryValue(query, field.name)}
                placeholder={field.placeholder}
              />
            )}
          </div>
        ))}
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href={`/reports/${slug}`}>
          Reset
        </Link>
      </div>
    </form>
  );
}

export function ReportResult({
  report,
  backHref,
}: Readonly<{
  report: LegacyReport;
  backHref: string;
}>) {
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="report-result-title">
      <div className="vehicle-page-header">
        <div>
          <p className="eyebrow">
            {report.totalCount} record{report.totalCount === 1 ? "" : "s"}
          </p>
          <h2 id="report-result-title">{report.title}</h2>
        </div>
        <div className="button-row">
          <Link className="button button-secondary" href={backHref}>
            Back to report menu
          </Link>
          <Link className="button button-secondary" href="/reports">
            Reports Menu
          </Link>
        </div>
      </div>
      {report.isApproximate && report.approximationReason ? (
        <div className="notice notice-info" role="status">
          {report.approximationReason}
        </div>
      ) : null}
      {report.rows.length === 0 ? (
        <div className="vehicle-empty-state">
          <p>No report rows found for the selected filters.</p>
        </div>
      ) : (
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">{report.title}</caption>
            <thead>
              <tr>
                {report.columns.map((column) => (
                  <th key={column.key} scope="col">
                    {column.header}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {report.rows.map((row, index) => (
                <tr key={`${report.reportKey}-${index}`}>
                  {report.columns.map((column) => (
                    <td key={column.key}>{row[column.key] ?? "-"}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
