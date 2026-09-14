import { redirect } from "next/navigation";

import { StreamedRoute } from "@/components/app-shell/streamed-route";

type Query = Record<string, string | string[] | undefined>;

function queryValue(query: Query, ...names: string[]) {
  for (const name of names) {
    const value = query[name];
    if (value !== undefined) return Array.isArray(value) ? (value[0] ?? "") : value;
  }
  return "";
}

function positiveInteger(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? String(parsed) : "";
}

function nonNegativeInteger(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed >= 0 ? String(parsed) : "";
}

function isoDate(value: string) {
  const trimmed = value.trim();
  if (/^\d{4}-\d{2}-\d{2}$/.test(trimmed)) return trimmed;
  const legacyParts = trimmed.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
  if (legacyParts) {
    const [, day, month, year] = legacyParts;
    const parsed = new Date(Date.UTC(Number(year), Number(month) - 1, Number(day)));
    if (
      parsed.getUTCFullYear() === Number(year) &&
      parsed.getUTCMonth() === Number(month) - 1 &&
      parsed.getUTCDate() === Number(day)
    )
      return `${year}-${month.padStart(2, "0")}-${day.padStart(2, "0")}`;
    return "";
  }
  const parsed = new Date(trimmed);
  return Number.isNaN(parsed.getTime()) ? "" : parsed.toISOString().slice(0, 10);
}

function outputPath(params: URLSearchParams) {
  return `/finance/reports/output?${params.toString()}`;
}

function legacyDetailPath(params: URLSearchParams) {
  return outputPath(
    new URLSearchParams({ kind: "legacy-detail", format: "html", ...Object.fromEntries(params) }),
  );
}

const INVOICE_ACTIONS: Record<string, string> = {
  summaryinvoicebycosttype: "summary-by-cost-type",
  summaryinvoicebycosttypeandjournal: "summary-by-cost-type-journal",
  summaryinvoicedreport: "summary-pdf",
  detailedinvoicedreport: "detailed-pdf",
  detailedinvoicedvipandtaxireport: "detailed-vip-taxi-pdf",
  fueldetailedinvoicedreport: "detailed-fuel-pdf",
  tollandoildetailedinvoicedreport: "detailed-toll-oil-pdf",
  surchargedetailedinvoicedreport: "detailed-surcharge-pdf",
};

const AUDIT_ACTIONS: Record<string, string> = {
  elsaudittrailreport: "els",
  manualkilosaudittrailreport: "manual-kilos",
  vehiclecontractsaudittrailreport: "contracts",
  viptaxiaudittrailreport: "vip-taxi",
};

function legacyOpenReportPath(query: Query) {
  const report = queryValue(query, "Report", "report").trim().toLowerCase();
  const filterBy = queryValue(query, "FilterBy", "filterBy").trim().toLowerCase();
  const id = positiveInteger(queryValue(query, "ID", "id"));
  const batchDate = isoDate(queryValue(query, "BatchDate", "batchDate"));

  const invoiceAction = INVOICE_ACTIONS[report];
  if (invoiceAction && id && batchDate) {
    if (filterBy === "province") {
      const params = new URLSearchParams({
        kind: "finance-menu",
        action: "province",
        reportAction: invoiceAction,
        province: id,
        batchDate,
        format: "html",
      });
      const departmentCode = positiveInteger(queryValue(query, "depCode", "DepartmentCode"));
      if (departmentCode) params.set("departmentCode", departmentCode);
      return outputPath(params);
    }

    return outputPath(
      new URLSearchParams({
        kind: "dedicated",
        reportAction: invoiceAction,
        id,
        batchDate,
        filterBy: filterBy === "site" ? "Site" : "Department",
        format: "html",
      }),
    );
  }

  if (report === "vehiclebillinghistory" || report === "vehiclebillinghistoryreport") {
    const financialYear = positiveInteger(queryValue(query, "FinYear", "finyear", "FinancialYear"));
    const registrationNumber = queryValue(query, "GGNo", "ggno", "RegistrationNumber").trim();
    if (financialYear && registrationNumber)
      return outputPath(
        new URLSearchParams({
          kind: "billing",
          financialYear,
          registrationNumber,
          format: "html",
        }),
      );
  }

  if (report === "summaryincomesplit" || report === "detailedincomesplit") {
    const financialYear = positiveInteger(queryValue(query, "FinYear", "finyear", "FinancialYear"));
    if (financialYear)
      return outputPath(
        new URLSearchParams({
          kind: "finance-menu",
          action:
            report === "summaryincomesplit" ? "income-split-summary" : "income-split-detailed",
          reportAction: "financial-year",
          financialYear,
          format: "html",
        }),
      );
  }

  if (report === "kilogaps") {
    const financialYear = positiveInteger(queryValue(query, "FinYear", "finyear", "FinancialYear"));
    if (financialYear)
      return outputPath(
        new URLSearchParams({
          kind: "missing-kilometres",
          action:
            queryValue(query, "OutputFormat", "outputformat").toLowerCase() === "xls"
              ? "kilo-gaps-xls"
              : "kilo-gaps-pdf",
          financialYear,
          format:
            queryValue(query, "OutputFormat", "outputformat").toLowerCase() === "xls"
              ? "excel"
              : "html",
        }),
      );
  }

  if (report === "reversalstreereport") {
    const journalNumber = queryValue(
      query,
      "ReversalJournalCode",
      "reversalJournalCode",
      "JournalNumber",
    ).trim();
    if (journalNumber)
      return outputPath(new URLSearchParams({ kind: "reversal", journalNumber, format: "html" }));
  }

  if (report === "numberoftripsonmontinterval") {
    return legacyDetailPath(new URLSearchParams({ item: "trip-number-interval" }));
  }

  if (report === "elslogreport") {
    const startDate = isoDate(queryValue(query, "StartDate", "startDate"));
    const endDate = isoDate(queryValue(query, "EndDate", "endDate"));
    if (startDate && endDate)
      return legacyDetailPath(new URLSearchParams({ item: "els-log", startDate, endDate }));
  }

  if (report === "vehiclesnocurrentcontractandfueltransactions") {
    return legacyDetailPath(new URLSearchParams({ item: "unallocated-vehicles" }));
  }

  if (report === "vehiclestatusreport") {
    const startDate = isoDate(queryValue(query, "StartDate", "startDate"));
    const endDate = isoDate(queryValue(query, "EndDate", "endDate"));
    const statusId = positiveInteger(queryValue(query, "ID", "id", "lstStatusID"));
    if (startDate && endDate && statusId)
      return legacyDetailPath(
        new URLSearchParams({ item: "vehicle-status", startDate, endDate, statusId }),
      );
  }

  const auditAction = AUDIT_ACTIONS[report];
  if (auditAction) {
    const startDate = isoDate(queryValue(query, "StartDate", "startDate"));
    const endDate = isoDate(queryValue(query, "EndDate", "endDate"));
    const mode = filterBy === "site" ? "site" : filterBy === "vehicle" ? "vehicle" : "department";
    const selectedId = queryValue(query, "ID", "id").trim();
    if (startDate && endDate && selectedId) {
      const params = new URLSearchParams({
        kind: "audit",
        action: mode,
        reportAction: auditAction,
        startDate,
        endDate,
        outputFormat: "pdf",
        format: "html",
      });
      if (mode === "department") params.set("departmentCode", positiveInteger(selectedId));
      else if (mode === "site") params.set("siteCode", positiveInteger(selectedId));
      else {
        params.set("vehicleNumber", selectedId);
        params.set(
          "numberType",
          queryValue(query, "NumType", "numType").trim().toLowerCase() === "gg" ? "gg" : "gp",
        );
      }
      return outputPath(params);
    }
  }

  if (report === "expendituretodateperdepartment") {
    // Legacy uses 0 for the all-departments selection; preserve it on direct print links.
    const departmentCode = nonNegativeInteger(queryValue(query, "depCode", "DepartmentCode", "ID"));
    if (departmentCode !== "")
      return outputPath(
        new URLSearchParams({
          kind: "outstanding",
          action: "department-site-vehicle",
          departmentCode,
          format: "html",
        }),
      );
  }

  return "/finance";
}

async function LegacyFinanceOpenReportContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>): Promise<never> {
  redirect(legacyOpenReportPath(await searchParams));
}

export default function LegacyFinanceOpenReportPage(
  props: Readonly<{ searchParams: Promise<Query> }>,
) {
  return (
    <StreamedRoute>
      <LegacyFinanceOpenReportContent {...props} />
    </StreamedRoute>
  );
}
