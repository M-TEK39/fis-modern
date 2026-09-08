import { NextResponse } from "next/server";

import { hasFinanceRole } from "@/app/finance/_components";
import { FinanceApiError, getFinanceOutput } from "@/lib/api-finance";
import { dedicatedFinanceReportPath, getAuditFinanceReport, getBillingHistory, getDedicatedFinanceReport, getRegionalFinanceReport, getReversalTree, getUniversalFinanceReport, getWesbankFinanceReport, REGIONAL_SUMMARY_ACTIONS, type FinanceReport } from "@/lib/api-finance-reports";
import { getSession } from "@/lib/session";

function positiveInteger(value: string | null) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function escapeHtml(value: unknown) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function htmlFor(report: FinanceReport) {
  const columns = report.rows.length > 0 ? Object.keys(report.rows[0]) : [];
  const headers = columns.map((column) => `<th>${escapeHtml(column.replaceAll("_", " "))}</th>`).join("");
  const body = report.rows.map((row) => `<tr>${columns.map((column) => `<td>${escapeHtml(row[column])}</td>`).join("")}</tr>`).join("");
  return `<!doctype html><html><head><meta charset="utf-8"><title>${escapeHtml(report.title)}</title><style>body{font-family:Arial,sans-serif;font-size:11pt}table{border-collapse:collapse;width:100%}th,td{border:1px solid #ccc;padding:4px 8px;text-align:left}th{background:#003366;color:#fff}tr:nth-child(even){background:#f2f2f2}@media print{@page{margin:1cm}}</style></head><body><h2>${escapeHtml(report.title)}</h2>${report.rows.length > 0 ? `<table><thead><tr>${headers}</tr></thead><tbody>${body}</tbody></table>` : "<p>No data found for the selected parameters.</p>"}</body></html>`;
}

function outputResponse(output: { body: ArrayBuffer; contentType: string; filename: string | null }, fallbackFilename: string, disposition: "attachment" | "inline") {
  const headers = new Headers({ "content-type": output.contentType });
  if (disposition === "inline") headers.set("content-disposition", "inline");
  else if (output.filename || fallbackFilename) headers.set("content-disposition", `attachment; filename="${(output.filename ?? fallbackFilename).replaceAll('"', "")}"`);
  return new NextResponse(output.body, { headers });
}

export async function GET(request: Request) {
  const session = await getSession();
  if (session.status !== "authenticated") return NextResponse.json({ message: "Authentication required." }, { status: 401 });
  if (!hasFinanceRole(session.roles)) return NextResponse.json({ message: "Finance permission required." }, { status: 403 });

  const query = new URL(request.url).searchParams;
  const kind = query.get("kind") ?? "";
  const format = query.get("format") ?? "html";
  try {
    if (kind === "dedicated") {
      const reportAction = query.get("reportAction") ?? "";
      const id = positiveInteger(query.get("id"));
      const postingMonthCode = positiveInteger(query.get("postingMonthCode"));
      const filterBy = query.get("filterBy") === "Site" ? "Site" : "Department";
      if (!getDedicatedFinanceReport(reportAction) || !id || !postingMonthCode || (format !== "html" && format !== "csv")) return NextResponse.json({ message: "Invalid Finance report output request." }, { status: 400 });
      const output = await getFinanceOutput(dedicatedFinanceReportPath(reportAction, { id, postingMonthCode, filterBy, format }));
      return outputResponse(output, `finance-${reportAction}.${format === "csv" ? "csv" : "html"}`, format === "html" ? "inline" : "attachment");
    }

    let report: FinanceReport;
    if (kind === "wesbank") {
      const action = query.get("action") ?? "";
      const reportAction = query.get("reportAction") ?? "";
      const validActions = ["summary-all", "summary-selection", "detailed-all", "detailed-selection"];
      const validReportActions = ["summary-all", "department-summary", "site-summary", "summary-all-download", "department-summary-download", "site-summary-download", "summary-province", "department-province-summary", "site-province-summary", "summary-province-download", "department-province-summary-download", "site-province-summary-download", "detailed-fuel-download", "detailed-other-download", "detailed-fuel-province-download", "detailed-other-province-download"];
      if (!validActions.includes(action) || !validReportActions.includes(reportAction) || (format !== "html" && format !== "excel")) return NextResponse.json({ message: "Invalid Wesbank report output request." }, { status: 400 });
      report = await getWesbankFinanceReport({ mode: action, provinceCode: query.get("provinceCode") ?? "", startDate: query.get("startDate") ?? "", endDate: query.get("endDate") ?? "" });
    } else if (kind === "regional") {
      const action = query.get("action") ?? "";
      const reportAction = query.get("reportAction") ?? "";
      if (!["summary-all", "summary-per-province"].includes(action) || !REGIONAL_SUMMARY_ACTIONS.includes(reportAction as (typeof REGIONAL_SUMMARY_ACTIONS)[number]) || (format !== "html" && format !== "excel")) return NextResponse.json({ message: "Invalid Regional Finance report output request." }, { status: 400 });
      const summaryType = reportAction.replace(/-download$/, "") === "summary"
        ? `SummaryReport${action === "summary-per-province" ? "PerProvince" : ""}`
        : reportAction.replace(/-download$/, "") === "department-cost-type"
          ? `SummaryReport${action === "summary-per-province" ? "PerProvince" : ""}DeptCostType`
          : `SummaryReport${action === "summary-per-province" ? "PerProvince" : ""}ByCostType`;
      report = await getRegionalFinanceReport({ mode: action, summaryType, provinceCode: query.get("provinceCode") ?? "", startDate: query.get("startDate") ?? "", endDate: query.get("endDate") ?? "" });
    } else if (kind === "billing") {
      const vmfCode = positiveInteger(query.get("vmfCode"));
      const financialYear = positiveInteger(query.get("financialYear"));
      if (!vmfCode || !financialYear) return NextResponse.json({ message: "A vehicle and financial year are required." }, { status: 400 });
      report = await getBillingHistory(vmfCode, financialYear);
    } else if (kind === "reversal") {
      const journalNumber = query.get("journalNumber")?.trim() ?? "";
      if (!journalNumber) return NextResponse.json({ message: "A journal number is required." }, { status: 400 });
      report = await getReversalTree(journalNumber);
    } else if (kind === "audit") {
      const auditType = query.get("reportAction") ?? "";
      const mode = query.get("action") ?? "department";
      const outputFormat = query.get("outputFormat") === "excel" ? "excel" : "pdf";
      if (!["els", "manual-kilos", "contracts", "vip-taxi"].includes(auditType) || !["department", "site", "vehicle"].includes(mode)) return NextResponse.json({ message: "Invalid audit report output request." }, { status: 400 });
      report = await getAuditFinanceReport({ mode, auditType, outputFormat, departmentCode: query.get("departmentCode") ?? "", siteCode: query.get("siteCode") ?? "", vmfCode: positiveInteger(query.get("vmfCode")), startDate: query.get("startDate") ?? "", endDate: query.get("endDate") ?? "" });
    } else if (kind === "universal") {
      report = await getUniversalFinanceReport({ mode: query.get("action") ?? "financial", action: query.get("reportAction") ?? "", departmentCode: query.get("departmentCode") ?? "", siteCode: query.get("siteCode") ?? "", province: query.get("province") ?? "", financialYear: query.get("financialYear") ?? "", batchDate: query.get("batchDate") ?? "", vmfCode: positiveInteger(query.get("vmfCode")), startDate: query.get("startDate") ?? "", endDate: query.get("endDate") ?? "" });
    } else {
      return NextResponse.json({ message: "Invalid Finance report output request." }, { status: 400 });
    }

    if (format === "excel") {
      const output = await getFinanceOutput("api/report/export/excel", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ data: report.rows, filename: `finance-${query.get("action") ?? "report"}-${Date.now()}.xlsx` }) });
      return outputResponse(output, `finance-${query.get("action") ?? "report"}.xlsx`, "attachment");
    }
    if (format !== "html") return NextResponse.json({ message: "Invalid Finance report output format." }, { status: 400 });
    return new NextResponse(htmlFor(report), { headers: { "content-type": "text/html; charset=utf-8", "content-disposition": "inline" } });
  } catch (error) {
    const status = error instanceof FinanceApiError && error.reason === "unauthorized" ? 401 : error instanceof FinanceApiError && error.reason === "invalid-response" ? 502 : 503;
    return NextResponse.json({ message: "The Finance report output could not be generated." }, { status });
  }
}
