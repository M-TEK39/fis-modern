import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import { redirect } from "next/navigation";
import { connection } from "next/server";
import {
  FinanceFrame,
  FinanceRestricted,
  FinanceUnavailable,
} from "@/app/(fleet-operations)/finance/_components";
import { hasFinanceRole } from "@/app/(fleet-operations)/finance/_utils";
import { departmentOptions, siteOptions } from "@/app/(fleet-operations)/finance/_location-options";
import { RegionalFinanceView } from "@/app/(fleet-operations)/finance/regional/[action]/regional-finance-view";
import { DepartmentApiError, getDepartments } from "@/lib/api/reference-data/api-departments";
import {
  FinanceApiError,
  getFinanceProvinces,
  type FinanceOption,
} from "@/lib/api/finance/api-finance";
import {
  getLegacyReport,
  LegacyReportApiError,
  type LegacyReport,
} from "@/lib/api/reports/api-legacy-reports";
import {
  getRegionalFinanceReport,
  REGIONAL_SUMMARY_ACTIONS,
  type FinanceReport,
} from "@/lib/api/finance/api-finance-reports";
import { SiteApiError, getSites } from "@/lib/api/reference-data/api-sites";
import { getSession } from "@/lib/auth/session";

type Query = Record<string, string | string[] | undefined>;
type PageProps = Readonly<{ params: Promise<{ action: string }>; searchParams: Promise<Query> }>;

const ACTIONS = [
  "summary-per-province",
  "summary-all",
  "assets-all",
  "assets-province",
  "assets-department",
  "assets-site",
] as const;

const SUMMARY_REPORTS: readonly {
  key: (typeof REGIONAL_SUMMARY_ACTIONS)[number];
  label: string;
  download: boolean;
}[] = [
  { key: "summary", label: "Show Summary Invoice Report", download: false },
  {
    key: "department-cost-type",
    label: "Show Summary Invoice Report Per Department By Cost Type",
    download: false,
  },
  {
    key: "site-cost-type",
    label: "Show Summary Invoice Report Per Department Per Site By Cost Type",
    download: false,
  },
  { key: "summary-download", label: "Download Summary Invoice Report", download: true },
  {
    key: "department-cost-type-download",
    label: "Download Summary Invoice Report Per Department By Cost Type",
    download: true,
  },
  {
    key: "site-cost-type-download",
    label: "Download Summary Invoice Report Per Department Per Site By Cost Type",
    download: true,
  },
];

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function titleFor(action: string) {
  return (
    (
      {
        "summary-per-province": "Summary Invoice Reports (Per Province)",
        "summary-all": "Summary Invoice Reports (All)",
        "assets-all": "Asset List: All Vehicles in FIS",
        "assets-province": "Asset List: Vehicles by Province",
        "assets-department": "Asset List: Vehicles by Department",
        "assets-site": "Asset List: Vehicles by Site",
      } as Record<string, string>
    )[action] ?? "Regional Module: Finance"
  );
}

function outputHref(action: string, reportAction: string, query: Query, format: "html" | "excel") {
  const params = new URLSearchParams({ kind: "regional", action, reportAction, format });
  for (const name of ["provinceCode", "startDate", "endDate"]) {
    const value = queryValue(query, name);
    if (value) params.set(name, value);
  }
  return `/finance/reports/output?${params.toString()}`;
}

function legacyToFinanceReport(report: LegacyReport): FinanceReport {
  return { title: report.title, rows: report.rows, supportsDateFilter: null };
}

function regionalSummaryType(action: string, reportAction: string) {
  const provincePrefix = action === "summary-per-province" ? "PerProvince" : "";
  return reportAction.replace(/-download$/, "") === "summary"
    ? `SummaryReport${provincePrefix}`
    : reportAction.replace(/-download$/, "") === "department-cost-type"
      ? `SummaryReport${provincePrefix}DeptCostType`
      : `SummaryReport${provincePrefix}ByCostType`;
}

const RegionalFinanceActionContent = renderRegionalFinanceActionContent;

async function renderRegionalFinanceActionContent({ params, searchParams }: PageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  const { action: rawAction } = await params;
  const action = rawAction.trim().toLowerCase();
  if (session.status !== "authenticated")
    return (
      <FinanceFrame title={titleFor(action)} description="Regional finance reporting.">
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasFinanceRole(session.roles))
    return (
      <FinanceFrame title={titleFor(action)} description="Regional finance reporting.">
        <FinanceRestricted />
      </FinanceFrame>
    );
  if (!ACTIONS.includes(action as (typeof ACTIONS)[number]))
    return (
      <FinanceFrame title="Regional Module: Finance" description="Regional finance reporting.">
        <FinanceRestricted message="The requested Regional Finance report is not available." />
      </FinanceFrame>
    );

  const query = await searchParams;
  const assetReport = action.startsWith("assets-");
  let departments: FinanceOption[] = [];
  let sites: FinanceOption[] = [];
  let provinces: FinanceOption[] = [];
  let error: string | null = null;
  try {
    if (assetReport) {
      departments = departmentOptions(await getDepartments());
      if (action === "assets-province") provinces = await getFinanceProvinces();
      if (action === "assets-site") {
        const departmentCode = Number(queryValue(query, "departmentCode"));
        const availableSites = await getSites();
        const selectedDepartment =
          Number.isSafeInteger(departmentCode) && departmentCode > 0 ? departmentCode : null;
        sites = siteOptions(
          selectedDepartment === null
            ? availableSites
            : availableSites.filter((site) => site.departmentCode === selectedDepartment),
        );
      }
    } else if (action === "summary-per-province") {
      provinces = await getFinanceProvinces();
    }
  } catch (caught) {
    if (
      caught instanceof FinanceApiError ||
      caught instanceof DepartmentApiError ||
      caught instanceof SiteApiError
    )
      error = caught.message;
    else throw caught;
  }

  let report: FinanceReport | null = null;
  let output: { href: string; label: string } | null = null;
  if (queryValue(query, "run") === "1") {
    if (assetReport) {
      const departmentCode = queryValue(query, "departmentCode");
      const provinceCode = queryValue(query, "provinceCode");
      const siteCode = queryValue(query, "siteCode");
      if (action === "assets-province" && !provinceCode)
        error = "Select a province before viewing the report.";
      else if (action === "assets-department" && !departmentCode)
        error = "Select a department before viewing the report.";
      else if (action === "assets-site" && (!departmentCode || !siteCode))
        error = "Select a department and site before viewing the report.";
      else {
        try {
          const legacy = await getLegacyReport("asset-list", {
            province: provinceCode || undefined,
            department: departmentCode || undefined,
            site: siteCode || undefined,
          });
          report = legacyToFinanceReport(legacy);
        } catch (caught) {
          if (caught instanceof LegacyReportApiError) error = caught.message;
          else throw caught;
        }
      }
    } else {
      const reportAction = queryValue(
        query,
        "reportAction",
      ) as (typeof REGIONAL_SUMMARY_ACTIONS)[number];
      const reportDefinition = SUMMARY_REPORTS.find((item) => item.key === reportAction);
      const startDate = queryValue(query, "startDate");
      const endDate = queryValue(query, "endDate");
      const provinceCode = queryValue(query, "provinceCode");
      if (!reportDefinition) error = "Select a Regional Finance report action.";
      else if (!startDate || !endDate)
        error = "Select a posting start date and end date before running the report.";
      else if (action === "summary-per-province" && !provinceCode)
        error = "Select a province before running the report.";
      else if (reportDefinition.download)
        output = {
          href: outputHref(action, reportDefinition.key, query, "excel"),
          label: "Download report",
        };
      else {
        try {
          report = await getRegionalFinanceReport({
            mode: action,
            summaryType: regionalSummaryType(action, reportDefinition.key),
            provinceCode,
            startDate,
            endDate,
          });
          output = {
            href: outputHref(action, reportDefinition.key, query, "html"),
            label: "Open printable report",
          };
        } catch (caught) {
          error =
            caught instanceof FinanceApiError
              ? caught.message
              : "The Regional Finance report could not be generated.";
        }
      }
    }
  }

  return (
    <RegionalFinanceView
      action={action}
      title={titleFor(action)}
      query={query}
      assetReport={assetReport}
      departments={departments}
      sites={sites}
      provinces={provinces}
      summaryReports={SUMMARY_REPORTS}
      error={error}
      output={output}
      report={report}
    />
  );
}

export default function RegionalFinanceActionPage(props: PageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <RegionalFinanceActionContent {...props} />
    </Suspense>
  );
}
