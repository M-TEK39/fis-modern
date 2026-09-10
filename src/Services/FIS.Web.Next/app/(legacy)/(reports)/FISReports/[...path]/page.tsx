import { redirect } from "next/navigation";

import ReportsPage from "@/app/(fleet-operations)/reports/page";
import { queryValue, type ReportQuery } from "@/app/(fleet-operations)/reports/_components";
import { ReportsRoutePage } from "@/app/(fleet-operations)/reports/[slug]/page";

type Props = Readonly<{
  params: Promise<{ path: string[] }>;
  searchParams: Promise<ReportQuery>;
}>;

export default async function LegacyFisReportsPage({ params, searchParams }: Props) {
  const { path } = await params;
  const route = path.join("/").toLowerCase();
  const query = await searchParams;

  if (route === "reports.aspx") return <ReportsPage />;
  if (route === "fis_report.aspx" || route === "../root/fis_report.aspx")
    return <ReportsRoutePage slug="fis-report" searchParams={Promise.resolve(query)} />;
  if (route === "tripreports.aspx")
    return <ReportsRoutePage slug="trip-authority" searchParams={Promise.resolve(query)} />;
  if (route === "contracts/contracts.aspx" || route === "fleetreportsmenu.aspx")
    return <ReportsRoutePage slug="contracts" searchParams={Promise.resolve(query)} />;

  const item =
    queryValue(query, "Item").trim().toLowerCase() ||
    queryValue(query, "item").trim().toLowerCase();
  const report =
    queryValue(query, "Report").trim().toLowerCase() ||
    queryValue(query, "report").trim().toLowerCase();
  if (route === "openreport_4.aspx") {
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
    if (reportAction) {
      const action = report.startsWith("summaryreportperprovince")
        ? "summary-per-province"
        : "summary-all";
      const params = new URLSearchParams({ run: "1", reportAction });
      for (const [name, aliases] of [
        ["startDate", ["StartDate", "startDate"]],
        ["endDate", ["EndDate", "endDate"]],
        ["provinceCode", ["Province", "province", "provinceCode"]],
      ] as const) {
        const value = aliases
          .map((alias) => queryValue(query, alias))
          .find((candidate) => candidate.trim());
        if (value) params.set(name, value);
      }
      redirect(`/finance/regional/${action}?${params.toString()}`);
    }
    redirect("/reports");
  }
  if (route === "openreport_3.aspx") {
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
    if (reportAction) {
      const action = report.startsWith("wesbankexpensesoneprovince")
        ? "summary-selection"
        : "summary-all";
      const params = new URLSearchParams({ run: "1", reportAction });
      for (const [name, aliases] of [
        ["startDate", ["StartDate", "startDate"]],
        ["endDate", ["EndDate", "endDate"]],
        ["provinceCode", ["Province", "province", "provinceCode"]],
      ] as const) {
        const value = aliases
          .map((alias) => queryValue(query, alias))
          .find((candidate) => candidate.trim());
        if (value) params.set(name, value);
      }
      redirect(`/finance/wesbank/${action}?${params.toString()}`);
    }
    redirect("/reports");
  }
  if (route === "downloadreport.aspx") {
    const regionalAction =
      item === "summaryreport"
        ? "summary-all"
        : item === "summaryreportdeptcosttype"
          ? "summary-all"
          : item === "summaryreportbycosttype"
            ? "summary-all"
            : item === "summaryreportperprovince"
              ? "summary-per-province"
              : item === "summaryreportperprovincedeptcosttype"
                ? "summary-per-province"
                : item === "summaryreportperprovincebycosttype"
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
          : item === "wesbankexpensesperprovinceperdepartmentsiteandmonth"
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
    if (kind) {
      const params = new URLSearchParams({
        kind,
        action: kind === "regional" ? regionalAction : wesbankAction,
        reportAction: kind === "regional" ? regionalReportAction : wesbankReportAction,
        format: "excel",
      });
      for (const [name, aliases] of [
        ["startDate", ["StartDate", "startDate"]],
        ["endDate", ["EndDate", "endDate"]],
        ["provinceCode", ["Province", "province", "provinceCode"]],
      ] as const) {
        const direct = aliases
          .map((alias) => queryValue(query, alias))
          .find((candidate) => candidate.trim());
        const numbered = Array.from({ length: 10 }, (_, index) => index + 1)
          .map((index) => ({
            name: queryValue(query, `ParamName${index}`).toLowerCase(),
            value: queryValue(query, `ParamValue${index}`),
          }))
          .find(
            (parameter) =>
              aliases.some((alias) => parameter.name === alias.toLowerCase()) &&
              parameter.value.trim(),
          )?.value;
        const value = direct || numbered;
        if (value) params.set(name, value);
      }
      redirect(`/finance/reports/output?${params.toString()}`);
    }
    redirect("/reports");
  }
  if (route === "openreport.aspx") {
    redirect(
      item === "vehiclebillinghistory"
        ? "/finance/reports/vehicle-billing-history"
        : item === "summaryincomesplit"
          ? "/finance/reports/income-split-summary"
          : item === "detailedincomesplit"
            ? "/finance/reports/income-split-detailed"
            : item === "kilogaps"
              ? "/finance/missing-kilometres/kilo-gaps-pdf"
              : "/reports",
    );
  }

  redirect(
    item === "journalswithinvalidbascodes"
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
                          : "/reports",
  );
}
