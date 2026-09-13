export type LegacyReportQuery = Record<string, string | string[] | undefined>;

type QueryKey = keyof LegacyReportQuery;

type LegacyPage = { kind: "reports" } | { kind: "route"; slug: string };

const REPORT_PARAMETER_ALIASES = [
  ["startDate", ["StartDate", "startDate"]],
  ["endDate", ["EndDate", "endDate"]],
  ["provinceCode", ["Province", "province", "provinceCode"]],
  ["departmentCode", ["DepartmentCode", "departmentCode", "DepartmentID", "DepID", "deptCode"]],
  ["siteCode", ["SiteCode", "siteCode", "SiteID", "siteID"]],
  ["financialYear", ["FinYear", "finyear", "FinancialYear", "financialYear"]],
  ["registrationNumber", ["GGNo", "ggno", "RegistrationNumber", "registrationNumber"]],
] as const;

function queryValue(query: LegacyReportQuery, key: QueryKey | string) {
  const value = query[key];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function firstQueryValue(query: LegacyReportQuery, aliases: readonly string[]) {
  return aliases.map((alias) => queryValue(query, alias)).find((value) => value.trim()) ?? "";
}

function withReportParameters(query: LegacyReportQuery, params: URLSearchParams) {
  for (const [name, aliases] of REPORT_PARAMETER_ALIASES) {
    const direct = firstQueryValue(query, aliases);
    const numbered = Array.from({ length: 10 }, (_, index) => index + 1)
      .map((index) => ({
        name: queryValue(query, `ParamName${index}`).toLowerCase(),
        value: queryValue(query, `ParamValue${index}`),
      }))
      .find(
        (parameter) =>
          aliases.some((alias) => parameter.name === alias.toLowerCase()) && parameter.value.trim(),
      )?.value;
    const value = direct || numbered;
    if (value) params.set(name, value);
  }
}

function pathWithQuery(path: string, params: URLSearchParams) {
  const query = params.toString();
  return query ? `${path}?${query}` : path;
}

function positiveInteger(value: string | null) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? String(parsed) : "";
}

function isoDate(value: string | null) {
  if (!value) return "";
  const trimmed = value.trim();
  if (/^\d{4}-\d{2}-\d{2}$/.test(trimmed)) return trimmed;
  const parts = trimmed.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
  if (!parts) return "";
  const [, day, month, year] = parts;
  const parsed = new Date(Date.UTC(Number(year), Number(month) - 1, Number(day)));
  return parsed.getUTCFullYear() === Number(year) &&
    parsed.getUTCMonth() === Number(month) - 1 &&
    parsed.getUTCDate() === Number(day)
    ? `${year}-${month.padStart(2, "0")}-${day.padStart(2, "0")}`
    : "";
}

function legacyDetailReportPath(query: LegacyReportQuery, item: string, format = "html") {
  const detailItem =
    item === "allroutesover25000km"
      ? "trip-routes-over-25000"
      : item === "alldaytripsover3500km"
        ? "trip-day-routes-over-3500"
        : item === "detailedinvoicefromjournalnumber"
          ? "journal-detailed-invoice"
          : item === "vehiclestatusreport"
            ? "vehicle-status"
            : "";
  if (!detailItem) return "";

  const params = new URLSearchParams({ kind: "legacy-detail", item: detailItem, format: "html" });
  withReportParameters(query, params);
  if (params.get("startDate")) params.set("startDate", isoDate(params.get("startDate")));
  if (params.get("endDate")) params.set("endDate", isoDate(params.get("endDate")));
  if (params.get("departmentCode"))
    params.set("departmentCode", positiveInteger(params.get("departmentCode")));
  if (params.get("siteCode")) params.set("siteCode", positiveInteger(params.get("siteCode")));
  if (detailItem === "journal-detailed-invoice") {
    const directJournal = firstQueryValue(query, ["JournalCode", "journalCode"]);
    const numberedJournal = Array.from({ length: 10 }, (_, index) => index + 1)
      .map((index) => ({
        name: queryValue(query, `ParamName${index}`).toLowerCase(),
        value: queryValue(query, `ParamValue${index}`),
      }))
      .find((parameter) => parameter.name === "journalcode")?.value;
    params.set("journalNumber", positiveInteger(directJournal || numberedJournal || ""));
  }
  if (detailItem === "vehicle-status") {
    const directStatus = firstQueryValue(query, ["ID", "id", "lstStatusID"]);
    const numberedStatus = Array.from({ length: 10 }, (_, index) => index + 1)
      .map((index) => ({
        name: queryValue(query, `ParamName${index}`).toLowerCase(),
        value: queryValue(query, `ParamValue${index}`),
      }))
      .find((parameter) => parameter.name === "id")?.value;
    params.set("statusId", positiveInteger(directStatus || numberedStatus || ""));
  }
  const required =
    detailItem === "journal-detailed-invoice"
      ? ["journalNumber"]
      : detailItem === "vehicle-status"
        ? ["statusId", "startDate", "endDate"]
        : ["departmentCode", "siteCode", "startDate", "endDate"];
  return required.some((name) => !params.get(name))
    ? ""
    : `/finance/reports/output?${new URLSearchParams({ ...Object.fromEntries(params), format }).toString()}`;
}

function directReportPath(
  path: string,
  query: LegacyReportQuery,
  requiredParameters: readonly string[] = [],
) {
  const params = new URLSearchParams({ run: "1" });
  withReportParameters(query, params);
  if (requiredParameters.some((name) => !params.get(name))) return path;
  return pathWithQuery(path, params);
}

function directFinancialYearReportPath(
  path: string,
  query: LegacyReportQuery,
  requiredParameters: readonly string[] = [],
) {
  const params = new URLSearchParams({ run: "1", reportAction: "financial-year" });
  withReportParameters(query, params);
  if (requiredParameters.some((name) => !params.get(name))) return path;
  return pathWithQuery(path, params);
}

function directAllocationPath(path: string, query: LegacyReportQuery) {
  const params = new URLSearchParams({ view: "search" });
  withReportParameters(query, params);
  return pathWithQuery(path, params);
}

function regionalOpenReportPath(query: LegacyReportQuery, report: string) {
  const reportAction =
    report === "summaryreportperprovince"
      ? "summary"
      : report === "summaryreportperprovincedeptcosttype"
        ? "department-cost-type"
        : report === "summaryreportperprovincebycosttype"
          ? "site-cost-type"
          : report === "summaryreportdeptcosttype"
            ? "department-cost-type"
            : report === "summaryreportbycosttype"
              ? "site-cost-type"
              : report === "summaryreport"
                ? "summary"
                : "";
  if (!reportAction) return "/reports";

  const action = report.startsWith("summaryreportperprovince")
    ? "summary-per-province"
    : "summary-all";
  const params = new URLSearchParams({ run: "1", reportAction });
  withReportParameters(query, params);
  return `/finance/regional/${action}?${params.toString()}`;
}

function wesbankOpenReportPath(query: LegacyReportQuery, report: string) {
  const reportAction =
    report === "wesbankexpensesperprovinceandmonth"
      ? "summary-all"
      : report === "wesbankexpensesperprovinceperdepartmentsubtotalandmonth"
        ? "department-summary"
        : report === "wesbankexpensesperprovinceperdepartmentsubtotalsiteandmonth"
          ? "site-summary"
          : report === "wesbankexpensesoneprovinceandallmonths"
            ? "summary-province"
            : report === "wesbankexpensesoneprovincealldepartmentsubtotalandmonth"
              ? "department-province-summary"
              : report === "wesbankexpensesoneprovincealldepartmentsiteandmonth"
                ? "site-province-summary"
                : "";
  if (!reportAction) return "/reports";

  const action = report.startsWith("wesbankexpensesoneprovince")
    ? "summary-selection"
    : "summary-all";
  const params = new URLSearchParams({ run: "1", reportAction });
  withReportParameters(query, params);
  return `/finance/wesbank/${action}?${params.toString()}`;
}

function downloadReportPath(query: LegacyReportQuery, item: string) {
  const regionalAction =
    item === "summaryreport" ||
    item === "summaryreportdeptcosttype" ||
    item === "summaryreportbycosttype"
      ? "summary-all"
      : item === "summaryreportperprovince" ||
          item === "summaryreportperprovincedeptcosttype" ||
          item === "summaryreportperprovincebycosttype"
        ? "summary-per-province"
        : "";
  const regionalReportAction =
    item === "summaryreport" || item === "summaryreportperprovince"
      ? "summary-download"
      : item === "summaryreportdeptcosttype" || item === "summaryreportperprovincedeptcosttype"
        ? "department-cost-type-download"
        : item === "summaryreportbycosttype" || item === "summaryreportperprovincebycosttype"
          ? "site-cost-type-download"
          : "";
  const wesbankAction = item.includes("oneprovince")
    ? "summary-selection"
    : item.includes("detailed") && item.includes("province")
      ? "detailed-selection"
      : item.includes("detailed")
        ? "detailed-all"
        : item.includes("wesbank")
          ? "summary-all"
          : "";
  const wesbankReportAction =
    item === "wesbankexpensesperprovinceandmonth"
      ? "summary-all-download"
      : item === "wesbankexpensesperprovinceperdepartmentandmonth"
        ? "department-summary-download"
        : item === "wesbankexpensesperdepartmentsiteandmonth"
          ? "site-summary-download"
          : item === "wesbankexpensesoneprovinceandallmonths"
            ? "summary-province-download"
            : item === "wesbankexpensesoneprovincealldepartmentsubtotalandmonth"
              ? "department-province-summary-download"
              : item === "wesbankexpensesoneprovincealldepartmentsiteandmonth"
                ? "site-province-summary-download"
                : item.includes("detailedfuel") && item.includes("oneprovince")
                  ? "detailed-fuel-province-download"
                  : item.includes("detailedother") && item.includes("oneprovince")
                    ? "detailed-other-province-download"
                    : item.includes("detailedfuel")
                      ? "detailed-fuel-download"
                      : item.includes("detailedother")
                        ? "detailed-other-download"
                        : "";
  const kind =
    regionalAction && regionalReportAction
      ? "regional"
      : wesbankAction && wesbankReportAction
        ? "wesbank"
        : "";
  if (!kind) return "/reports";

  const params = new URLSearchParams({
    kind,
    action: kind === "regional" ? regionalAction : wesbankAction,
    reportAction: kind === "regional" ? regionalReportAction : wesbankReportAction,
    format: "excel",
  });
  withReportParameters(query, params);
  return `/finance/reports/output?${params.toString()}`;
}

function openReportPath(query: LegacyReportQuery, item: string) {
  return item === "vehiclebillinghistory"
    ? directFinancialYearReportPath("/finance/reports/vehicle-billing-history", query, [
        "financialYear",
        "registrationNumber",
      ])
    : item === "summaryincomesplit"
      ? directFinancialYearReportPath("/finance/reports/income-split-summary", query, [
          "financialYear",
        ])
      : item === "detailedincomesplit"
        ? directFinancialYearReportPath("/finance/reports/income-split-detailed", query, [
            "financialYear",
          ])
        : item === "kilogaps"
          ? directReportPath(
              (
                queryValue(query, "OutputFormat") || queryValue(query, "outputformat")
              ).toLowerCase() === "xls"
                ? "/finance/missing-kilometres/kilo-gaps-xls"
                : "/finance/missing-kilometres/kilo-gaps-pdf",
              query,
              ["financialYear"],
            )
          : "/reports";
}

function defaultReportPath(query: LegacyReportQuery, item: string) {
  const detailPath = legacyDetailReportPath(query, item);
  if (detailPath) return detailPath;
  if (item === "vehiclestatusreport") return "/reports/vehicle-status-range";
  return item === "journalswithinvalidbascodes"
    ? directAllocationPath("/finance/financial-allocation/invalid-journals", query)
    : item === "departmentswithnobascodes"
      ? directAllocationPath("/finance/financial-allocation/departments-no-bas", query)
      : item === "departmentswithmissingfinancialsystem"
        ? directAllocationPath(
            "/finance/financial-allocation/departments-missing-financial-system",
            query,
          )
        : item === "alloutstandingamountsperdepartment"
          ? directReportPath("/finance/outstanding/department", query)
          : item === "alloutstandingamountsperdepartmentandsite"
            ? directReportPath("/finance/outstanding/department-site", query)
            : item === "alloutstandingamountsatmonthendpervehicle"
              ? directReportPath("/finance/outstanding/month-end-vehicle", query)
              : item === "allocationexception"
                ? directReportPath("/finance/outstanding/allocation-exception", query)
                : item === "comparebilledkilosandfuelconsumption"
                  ? directReportPath("/finance/missing-kilometres/fuel-consumption", query, [
                      "startDate",
                      "endDate",
                    ])
                  : item === "vehicleswithnokilosconsumingfuel"
                    ? directReportPath(
                        "/finance/missing-kilometres/no-kilos-consuming-fuel",
                        query,
                        ["startDate", "endDate"],
                      )
                    : item === "exportpastelcsv"
                      ? "/finance/interface/pastel-csv"
                      : item === "exportpastelcsvwithclient"
                        ? "/finance/interface/pastel-csv-customer"
                        : "/reports";
}

export function legacyPageForRoute(route: string): LegacyPage | null {
  if (route === "reports.aspx") return { kind: "reports" };
  if (route === "fis_report.aspx" || route === "../root/fis_report.aspx")
    return { kind: "route", slug: "fis-report" };
  if (route === "tripreports.aspx") return { kind: "route", slug: "trip-authority" };
  if (route === "contracts/contracts.aspx" || route === "fleetreportsmenu.aspx")
    return { kind: "route", slug: "contracts" };
  return null;
}

export function legacyRedirectPath(route: string, query: LegacyReportQuery) {
  const item =
    queryValue(query, "Item").trim().toLowerCase() ||
    queryValue(query, "item").trim().toLowerCase();
  const report =
    queryValue(query, "Report").trim().toLowerCase() ||
    queryValue(query, "report").trim().toLowerCase();

  if (route === "openreport_4.aspx") return regionalOpenReportPath(query, report);
  if (route === "openreport_3.aspx") return wesbankOpenReportPath(query, report);
  if (route === "downloadreport.aspx")
    return legacyDetailReportPath(query, item, "excel") || downloadReportPath(query, item);
  if (route === "openreport.aspx") return openReportPath(query, item);
  return defaultReportPath(query, item);
}
