import { NextResponse } from "next/server";

import { hasGeneralFinanceReportsAccess } from "@/app/(fleet-operations)/finance/_utils";
import { FinanceApiError, getFinanceOutput, runFinanceAction } from "@/lib/api/finance/api-finance";
import {
  dedicatedFinanceReportPath,
  getAuditFinanceReport,
  getBillingHistory,
  getDedicatedFinanceReportData,
  getDedicatedFinanceReport,
  getLegacyFinanceDetailReport,
  getMissingKilometresFinanceReport,
  getRegionalFinanceReport,
  getReversalTree,
  getFinanceMenuReport,
  getWesbankFinanceReport,
  mapFinanceReport,
  REGIONAL_SUMMARY_ACTIONS,
  type FinanceReport,
} from "@/lib/api/finance/api-finance-reports";
import {
  getLegacyReport,
  LegacyReportApiError,
  type LegacyReport,
} from "@/lib/api/reports/api-legacy-reports";
import { getSession } from "@/lib/auth/session";

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

type PrintLayout = Readonly<{
  orientation: "portrait" | "landscape";
  columnsPerPage: number;
}>;

const PORTRAIT_PRINT_LAYOUT: PrintLayout = { orientation: "portrait", columnsPerPage: 6 };
const LANDSCAPE_PRINT_LAYOUT: PrintLayout = { orientation: "landscape", columnsPerPage: 8 };
const OUTSTANDING_REPORT_TITLES: Record<string, string> = {
  "department-site-vehicle": "Outstanding Amounts per Department, Site and Vehicle",
  department: "Outstanding Amounts per Department",
  "department-site": "Outstanding Amounts per Department and Site",
  "month-end-vehicle": "Outstanding Amounts at Month End per Vehicle",
  "allocation-exception": "Allocation Exceptions (Un-Interfaced Transactions)",
};
const LEGACY_ASSET_REPORT_KEYS = new Set([
  "asset-list",
  "asset-list-by-province",
  "asset-list-by-department",
  "asset-list-by-site",
]);

type PrintableReport = Readonly<{
  title: string;
  rows: Array<Record<string, unknown>>;
  columns?: Array<{ key: string; header: string }>;
}>;

/**
 * The legacy Finance reports did not choose paper orientation from whichever
 * columns happened to be returned. Their ActiveReports definitions have an
 * explicit A4 page setting: invoices, billing history, income and kilo gaps
 * are portrait, while Wesbank, regional, audit-trail and reversal reports are
 * landscape. Keep that contract here so a schema change cannot silently turn
 * a previously printable report into a squashed page.
 */
function printLayoutFor(
  kind: string,
  action: string,
  reportAction: string,
  item: string,
): PrintLayout {
  if (kind === "legacy-asset") return LANDSCAPE_PRINT_LAYOUT;

  if (["wesbank", "regional", "audit", "reversal"].includes(kind)) {
    return LANDSCAPE_PRINT_LAYOUT;
  }

  if (kind === "legacy-detail") {
    return item === "els-log" ? LANDSCAPE_PRINT_LAYOUT : PORTRAIT_PRINT_LAYOUT;
  }

  if (
    (kind === "dedicated" || kind === "finance-menu") &&
    ["detailed-fuel", "detailed-toll-oil", "detailed-surcharge"].some((prefix) =>
      reportAction.startsWith(prefix),
    )
  ) {
    return LANDSCAPE_PRINT_LAYOUT;
  }

  if (["dedicated", "billing", "missing-kilometres"].includes(kind)) {
    return PORTRAIT_PRINT_LAYOUT;
  }

  if (kind === "finance-menu" && action === "profitability") {
    return LANDSCAPE_PRINT_LAYOUT;
  }

  // The remaining Finance menu reports are retained income layouts, which
  // are A4 portrait. `reportAction` describes the selected filter and must
  // not be used to infer paper orientation.
  return PORTRAIT_PRINT_LAYOUT;
}

function printableLegacyAssetReport(report: LegacyReport): PrintableReport {
  return {
    title: report.title,
    columns: report.columns,
    rows: report.rows.map((row) => ({ ...row })),
  };
}

function htmlFor(report: PrintableReport, layout: PrintLayout) {
  const columns =
    report.columns?.map((column) => column.key) ??
    (report.rows.length > 0 ? Object.keys(report.rows[0]) : []);
  const columnHeader = (column: string) =>
    report.columns?.find((item) => item.key === column)?.header ?? column.replaceAll("_", " ");
  const groups = Array.from(
    { length: Math.max(1, Math.ceil(columns.length / layout.columnsPerPage)) },
    (_, index) => columns.slice(index * layout.columnsPerPage, (index + 1) * layout.columnsPerPage),
  );
  const tables = report.rows.length
    ? groups
        .map((group, index) => {
          const headers = group
            .map((column) => `<th scope="col">${escapeHtml(columnHeader(column))}</th>`)
            .join("");
          const body = report.rows
            .map(
              (row) =>
                `<tr>${group.map((column) => `<td>${escapeHtml(row[column])}</td>`).join("")}</tr>`,
            )
            .join("");
          const continuation =
            groups.length > 1
              ? `<h2>${escapeHtml(report.title)} — fields ${index * layout.columnsPerPage + 1}–${index * layout.columnsPerPage + group.length} of ${columns.length}</h2>`
              : "";
          return `<section class="report-section${index ? " report-section-break" : ""}">${continuation}<table><thead><tr>${headers}</tr></thead><tbody>${body}</tbody></table></section>`;
        })
        .join("")
    : '<p class="empty">No data found for the selected parameters.</p>';
  const { orientation } = layout;
  return `<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>${escapeHtml(report.title)}</title><style>@page{size:A4 ${orientation};margin:10mm}*{box-sizing:border-box}body{margin:0;color:#111827;background:#fff;font-family:Arial,sans-serif;font-size:9pt;line-height:1.35}.screen-actions{margin:0 0 8mm}button{border:1px solid #172554;border-radius:4px;background:#172554;color:#fff;cursor:pointer;padding:7px 12px;font-weight:700}.letterhead{display:grid;grid-template-columns:45mm 1fr;gap:6mm;align-items:center;border-bottom:1px solid #1e3a8a;padding:0 0 4mm;color:#1e3a8a}.letterhead img{display:block;width:43mm;height:auto}.letterhead p{margin:0.4mm 0;font-size:7pt}.letterhead strong{font-size:8pt}.document-title{margin:5mm 0 1mm;color:#172554;font-size:14pt}.document-meta{margin:0 0 5mm;color:#475569;font-size:8pt}.report-section-break{break-before:page}.report-section h2{margin:0 0 3mm;color:#172554;font-size:11pt}table{width:100%;border-collapse:collapse;table-layout:auto}th,td{border:1px solid #94a3b8;padding:1.7mm;text-align:left;vertical-align:top;overflow-wrap:anywhere;word-break:normal}th{background:#dbeafe;color:#172554;font-size:7pt;font-weight:700;line-height:1.15;text-transform:uppercase}tr{break-inside:avoid}.empty{margin:8mm 0}@media print{.screen-actions{display:none}thead{display:table-header-group}th{-webkit-print-color-adjust:exact;print-color-adjust:exact}}</style></head><body><div class="screen-actions"><button type="button" onclick="window.print()">Print report</button></div><header class="letterhead"><img alt="Gauteng Province Roads and Transport" src="/logo/gauteng-province-roads-and-transport.jpg"><div><strong>DEPARTMENT OF PUBLIC TRANSPORT, ROADS AND WORKS</strong><p>DEPARTEMENT VAN OPENBARE VERVOER, PAAIE EN WERKE</p><p>LEFAPHA DIPALANGWA TSA SETJHABA, DITSELA LE MESEBTSI</p><p>UMNYANGO WEZOKUTHUTHA WOMPHAKATHI, EZEMIGWAQO NEZEMSEBENZI</p></div></header><main><h1 class="document-title">${escapeHtml(report.title)}</h1><p class="document-meta">Fleet Information System · ${report.rows.length} record${report.rows.length === 1 ? "" : "s"} · A4 ${orientation}</p>${tables}</main></body></html>`;
}

function outputResponse(
  output: { body: ArrayBuffer; contentType: string; filename: string | null },
  fallbackFilename: string,
  disposition: "attachment" | "inline",
) {
  const headers = new Headers({ "content-type": output.contentType });
  if (disposition === "inline") headers.set("content-disposition", "inline");
  else if (output.filename || fallbackFilename)
    headers.set(
      "content-disposition",
      `attachment; filename="${(output.filename ?? fallbackFilename).replaceAll('"', "")}"`,
    );
  return new NextResponse(output.body, { headers });
}

export async function GET(request: Request) {
  const session = await getSession();
  if (session.status !== "authenticated")
    return NextResponse.json({ message: "Authentication required." }, { status: 401 });
  if (!hasGeneralFinanceReportsAccess(session.roles))
    return NextResponse.json({ message: "Finance permission required." }, { status: 403 });

  const query = new URL(request.url).searchParams;
  const kind = query.get("kind") ?? "";
  const format = query.get("format") ?? "html";
  const printLayout = printLayoutFor(
    kind,
    query.get("action") ?? "",
    query.get("reportAction") ?? "",
    query.get("item") ?? "",
  );
  try {
    if (kind === "interface") {
      const action = query.get("action") ?? "";
      const batchDate = query.get("batchDate") ?? "";
      if (!["pastel-csv", "pastel-csv-customer"].includes(action) || !batchDate)
        return NextResponse.json(
          { message: "A valid interface action and batch date are required." },
          { status: 400 },
        );
      const endpoint =
        action === "pastel-csv-customer"
          ? "api/finance/interface/pastel-csv-customer"
          : "api/finance/interface/pastel-csv";
      const output = await getFinanceOutput(endpoint, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ batchDate, financialSystemCode: 1 }),
      });
      return outputResponse(
        output,
        `${action === "pastel-csv-customer" ? "pastel-customers" : "pastel-export"}-${Date.now()}.csv`,
        "attachment",
      );
    }

    if (kind === "legacy-asset") {
      const reportKey = query.get("reportKey") ?? "";
      if (!LEGACY_ASSET_REPORT_KEYS.has(reportKey) || format !== "html")
        return NextResponse.json({ message: "Invalid Asset List print request." }, { status: 400 });

      const report = await getLegacyReport(
        reportKey,
        {
          province: query.get("province") ?? undefined,
          department: query.get("department") ?? undefined,
          site: query.get("site") ?? undefined,
        },
        { includeAll: true },
      );
      return new NextResponse(htmlFor(printableLegacyAssetReport(report), printLayout), {
        headers: {
          "content-type": "text/html; charset=utf-8",
          "content-disposition": "inline",
        },
      });
    }

    if (kind === "dedicated") {
      const reportAction = query.get("reportAction") ?? "";
      const id = positiveInteger(query.get("id"));
      const batchDate = query.get("batchDate") ?? "";
      const filterBy = query.get("filterBy") === "Site" ? "Site" : "Department";
      if (
        !getDedicatedFinanceReport(reportAction) ||
        !id ||
        !/^\d{4}-\d{2}-\d{2}$/.test(batchDate) ||
        (format !== "html" && format !== "csv")
      )
        return NextResponse.json(
          { message: "Invalid Finance report output request." },
          { status: 400 },
        );
      if (format === "html") {
        const report = await getDedicatedFinanceReportData(reportAction, {
          id,
          batchDate,
          filterBy,
        });
        return new NextResponse(htmlFor(report, printLayout), {
          headers: {
            "content-type": "text/html; charset=utf-8",
            "content-disposition": "inline",
          },
        });
      }
      const output = await getFinanceOutput(
        dedicatedFinanceReportPath(reportAction, { id, batchDate, filterBy, format }),
      );
      return outputResponse(
        output,
        `finance-${reportAction}.${format === "csv" ? "csv" : "html"}`,
        "attachment",
      );
    }

    let report: FinanceReport;
    if (kind === "wesbank") {
      const action = query.get("action") ?? "";
      const reportAction = query.get("reportAction") ?? "";
      const validActions = [
        "summary-all",
        "summary-selection",
        "detailed-all",
        "detailed-selection",
      ];
      const validReportActions = [
        "summary-all",
        "department-summary",
        "site-summary",
        "summary-all-download",
        "department-summary-download",
        "site-summary-download",
        "summary-province",
        "department-province-summary",
        "site-province-summary",
        "summary-province-download",
        "department-province-summary-download",
        "site-province-summary-download",
        "detailed-fuel-download",
        "detailed-other-download",
        "detailed-fuel-province-download",
        "detailed-other-province-download",
      ];
      if (
        !validActions.includes(action) ||
        !validReportActions.includes(reportAction) ||
        (format !== "html" && format !== "excel")
      )
        return NextResponse.json(
          { message: "Invalid Wesbank report output request." },
          { status: 400 },
        );
      report = await getWesbankFinanceReport({
        mode: action,
        reportAction,
        provinceCode: query.get("provinceCode") ?? "",
        startDate: query.get("startDate") ?? "",
        endDate: query.get("endDate") ?? "",
      });
    } else if (kind === "regional") {
      const action = query.get("action") ?? "";
      const reportAction = query.get("reportAction") ?? "";
      if (
        !["summary-all", "summary-per-province"].includes(action) ||
        !REGIONAL_SUMMARY_ACTIONS.includes(
          reportAction as (typeof REGIONAL_SUMMARY_ACTIONS)[number],
        ) ||
        (format !== "html" && format !== "excel")
      )
        return NextResponse.json(
          { message: "Invalid Regional Finance report output request." },
          { status: 400 },
        );
      const summaryType =
        reportAction.replace(/-download$/, "") === "summary"
          ? `SummaryReport${action === "summary-per-province" ? "PerProvince" : ""}`
          : reportAction.replace(/-download$/, "") === "department-cost-type"
            ? `SummaryReport${action === "summary-per-province" ? "PerProvince" : ""}DeptCostType`
            : `SummaryReport${action === "summary-per-province" ? "PerProvince" : ""}ByCostType`;
      report = await getRegionalFinanceReport({
        mode: action,
        summaryType,
        provinceCode: query.get("provinceCode") ?? "",
        startDate: query.get("startDate") ?? "",
        endDate: query.get("endDate") ?? "",
      });
    } else if (kind === "billing") {
      const registrationNumber = query.get("registrationNumber")?.trim() ?? "";
      const financialYear = positiveInteger(query.get("financialYear"));
      if (!registrationNumber || !financialYear)
        return NextResponse.json(
          { message: "A vehicle registration number and financial year are required." },
          { status: 400 },
        );
      report = await getBillingHistory(registrationNumber, financialYear);
    } else if (kind === "reversal") {
      const journalNumber = query.get("journalNumber")?.trim() ?? "";
      if (!journalNumber)
        return NextResponse.json({ message: "A journal number is required." }, { status: 400 });
      report = await getReversalTree(journalNumber);
    } else if (kind === "outstanding") {
      const action = query.get("action") ?? "";
      if (
        ![
          "department-site-vehicle",
          "department",
          "department-site",
          "month-end-vehicle",
          "allocation-exception",
        ].includes(action)
      )
        return NextResponse.json(
          { message: "Invalid outstanding report output request." },
          { status: 400 },
        );
      report = mapFinanceReport(
        await runFinanceAction("api/report/finance/outstanding", {
          mode: action,
          departmentCode: query.get("departmentCode") ?? "",
          siteCode: query.get("siteCode") ?? "",
          financialYear: query.get("financialYear") ?? "",
          vmfCode: positiveInteger(query.get("vmfCode")),
        }),
        OUTSTANDING_REPORT_TITLES[action] ?? "Outstanding Amounts Report",
      );
    } else if (kind === "audit") {
      const auditType = query.get("reportAction") ?? "";
      const mode = query.get("action") ?? "department";
      const outputFormat = query.get("outputFormat") === "excel" ? "excel" : "pdf";
      if (
        !["els", "manual-kilos", "contracts", "vip-taxi"].includes(auditType) ||
        !["department", "site", "vehicle"].includes(mode)
      )
        return NextResponse.json(
          { message: "Invalid audit report output request." },
          { status: 400 },
        );
      report = await getAuditFinanceReport({
        mode,
        auditType,
        outputFormat,
        departmentCode: query.get("departmentCode") ?? "",
        siteCode: query.get("siteCode") ?? "",
        vmfCode: positiveInteger(query.get("vmfCode")),
        vehicleNumber: query.get("vehicleNumber") ?? "",
        numberType: query.get("numberType") === "gg" ? "gg" : "gp",
        startDate: query.get("startDate") ?? "",
        endDate: query.get("endDate") ?? "",
      });
    } else if (kind === "legacy-detail") {
      const item = query.get("item") ?? "";
      const validItems = new Set([
        "wesbank-site-vehicle-detail",
        "wesbank-registration-number-detail",
        "wesbank-vehicle-detail",
        "regional-total-cost-province",
        "regional-total-cost-province-department",
        "regional-total-cost-province-department-site",
        "regional-total-cost-province-department-site-cost-type",
        "journal-detailed-invoice",
        "trip-routes-over-25000",
        "trip-day-routes-over-3500",
        "trip-number-interval",
        "els-log",
        "unallocated-vehicles",
        "vehicle-status",
      ]);
      const startDate = query.get("startDate") ?? "";
      const endDate = query.get("endDate") ?? "";
      const departmentCode = positiveInteger(query.get("departmentCode"));
      const siteCode = positiveInteger(query.get("siteCode"));
      const journalNumber = positiveInteger(query.get("journalNumber"));
      const statusId = positiveInteger(query.get("statusId"));
      const province = query.get("province")?.trim() ?? "";
      const registrationNumber = query.get("registrationNumber")?.trim() ?? "";
      if (
        !validItems.has(item) ||
        (format !== "html" && format !== "excel") ||
        (startDate && !/^\d{4}-\d{2}-\d{2}$/.test(startDate)) ||
        (endDate && !/^\d{4}-\d{2}-\d{2}$/.test(endDate))
      )
        return NextResponse.json(
          { message: "Invalid legacy Finance detail report request." },
          { status: 400 },
        );
      report = await getLegacyFinanceDetailReport({
        item,
        startDate: startDate || undefined,
        endDate: endDate || undefined,
        departmentCode,
        siteCode,
        province: province || undefined,
        journalNumber,
        registrationNumber: registrationNumber || undefined,
        statusId,
      });
    } else if (kind === "finance-menu") {
      report = await getFinanceMenuReport({
        mode: query.get("action") ?? "financial",
        action: query.get("reportAction") ?? "",
        departmentCode: query.get("departmentCode") ?? "",
        siteCode: query.get("siteCode") ?? "",
        province: query.get("province") ?? "",
        financialYear: query.get("financialYear") ?? "",
        batchDate: query.get("batchDate") ?? "",
        vmfCode: positiveInteger(query.get("vmfCode")),
        startDate: query.get("startDate") ?? "",
        endDate: query.get("endDate") ?? "",
      });
    } else if (kind === "missing-kilometres") {
      const action = query.get("action") ?? "";
      if (!["kilo-gaps-pdf", "kilo-gaps-xls"].includes(action) || !query.get("financialYear"))
        return NextResponse.json(
          { message: "A valid missing-kilometres action and financial year are required." },
          { status: 400 },
        );
      report = await getMissingKilometresFinanceReport({
        mode: action,
        financialYear: query.get("financialYear") ?? "",
      });
    } else {
      return NextResponse.json(
        { message: "Invalid Finance report output request." },
        { status: 400 },
      );
    }

    if (format === "excel") {
      const output = await getFinanceOutput("api/report/export/excel", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          data: report.rows,
          filename: `finance-${query.get("action") ?? "report"}-${Date.now()}.xlsx`,
        }),
      });
      return outputResponse(
        output,
        `finance-${query.get("action") ?? "report"}.xlsx`,
        "attachment",
      );
    }
    if (format !== "html")
      return NextResponse.json(
        { message: "Invalid Finance report output format." },
        { status: 400 },
      );
    return new NextResponse(htmlFor(report, printLayout), {
      headers: { "content-type": "text/html; charset=utf-8", "content-disposition": "inline" },
    });
  } catch (error) {
    const status =
      (error instanceof FinanceApiError || error instanceof LegacyReportApiError) &&
      error.reason === "unauthorized"
        ? 401
        : (error instanceof FinanceApiError || error instanceof LegacyReportApiError) &&
            error.reason === "invalid-response"
          ? 502
          : 503;
    return NextResponse.json(
      { message: "The Finance report output could not be generated." },
      { status },
    );
  }
}
