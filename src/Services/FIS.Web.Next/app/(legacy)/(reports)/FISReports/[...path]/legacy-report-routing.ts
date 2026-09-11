export type LegacyReportQuery = Record<string, string | string[] | undefined>;

type QueryKey = keyof LegacyReportQuery;

type LegacyPage = { kind: "reports" } | { kind: "route"; slug: string };

const REPORT_PARAMETER_ALIASES = [
  ["startDate", ["StartDate", "startDate"]],
  ["endDate", ["EndDate", "endDate"]],
  ["provinceCode", ["Province", "province", "provinceCode"]],
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

function openReportPath(item: string) {
  return item === "vehiclebillinghistory"
    ? "/finance/reports/vehicle-billing-history"
    : item === "summaryincomesplit"
      ? "/finance/reports/income-split-summary"
      : item === "detailedincomesplit"
        ? "/finance/reports/income-split-detailed"
        : item === "kilogaps"
          ? "/finance/missing-kilometres/kilo-gaps-pdf"
          : "/reports";
}

function defaultReportPath(item: string) {
  return item === "journalswithinvalidbascodes"
    ? "/finance/financial-allocation/invalid-journals"
    : item === "departmentswithnobascodes"
      ? "/finance/financial-allocation/departments-no-bas"
      : item === "departmentswithmissingfinancialsystem"
        ? "/finance/financial-allocation/departments-missing-financial-system"
        : item === "alloutstandingamountsperdepartment"
          ? "/finance/outstanding/department"
          : item === "alloutstandingamountsperdepartmentandsite"
            ? "/finance/outstanding/department-site"
            : item === "alloutstandingamountsatmonthendpervehicle"
              ? "/finance/outstanding/month-end-vehicle"
              : item === "allocationexception"
                ? "/finance/outstanding/allocation-exception"
                : item === "comparebilledkilosandfuelconsumption"
                  ? "/finance/missing-kilometres/fuel-consumption"
                  : item === "vehicleswithnokilosconsumingfuel"
                    ? "/finance/missing-kilometres/no-kilos-consuming-fuel"
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
  if (route === "downloadreport.aspx") return downloadReportPath(query, item);
  if (route === "openreport.aspx") return openReportPath(item);
  return defaultReportPath(item);
}
