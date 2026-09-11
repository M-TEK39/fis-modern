import { connection } from "next/server";
import { redirect } from "next/navigation";
import Link from "next/link";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import {
  AccessRestricted,
  ReportFilterForm,
  ReportMenu,
  ReportResult,
  ReportsFrame,
  ReportsUnavailable,
} from "@/app/(fleet-operations)/reports/_components";
import {
  hasReportsRole,
  queryValue,
  REPORT_MENU_ENTRIES,
} from "@/app/(fleet-operations)/reports/_utils";
import type {
  ReportField,
  ReportMenuEntry,
  ReportQuery,
} from "@/app/(fleet-operations)/reports/_utils";
import { getLegacyReport, LegacyReportApiError } from "@/lib/api/reports/api-legacy-reports";
import { getSession } from "@/lib/auth/session";

type ReportRouteProps = Readonly<{
  slug: string;
  searchParams: Promise<ReportQuery>;
  forcedReportKey?: string;
}>;

type ReportDefinition = {
  title: string;
  description: string;
  menu?: readonly ReportMenuEntry[];
  reportKey?: string;
  resolveReportKey?: (query: ReportQuery) => string;
  fields?: readonly ReportField[];
  autoLoad?: boolean;
};

const SEARCH_FIELDS: readonly ReportField[] = [
  {
    name: "mode",
    label: "Number Type",
    options: [
      { value: "GG", label: "GG" },
      { value: "GP", label: "GP" },
      { value: "ENGINE", label: "Engine" },
      { value: "VIN", label: "VIN / Chassis" },
    ],
  },
  { name: "search", label: "Search", placeholder: "Enter GG, GP, Engine or VIN/Chassis" },
];

const DATE_FIELDS: readonly ReportField[] = [
  { name: "from", label: "From Date", type: "date" },
  { name: "to", label: "To Date", type: "date" },
];

const REPORT_DEFINITIONS: Record<string, ReportDefinition> = {
  "asset-list": {
    title: "Asset List",
    description: "Legacy asset list submenu with compatible vehicle and status data.",
    menu: REPORT_MENU_ENTRIES.assetList,
    reportKey: "asset-list",
  },
  "audit-trail": {
    title: "Audit Trail Reports",
    description: "Legacy audit trail report menu.",
    menu: REPORT_MENU_ENTRIES.auditTrail,
    resolveReportKey: (query) =>
      ({
        department: "audit-trail-department",
        site: "audit-trail-site",
        vehicle: "audit-trail-vehicle",
      })[queryValue(query, "rtype")] ?? "audit-trail",
  },
  "class-code": {
    title: "Class Code Reports",
    description: "Legacy class-code report menu.",
    menu: [
      { label: "1) Class codes with tariffs", key: "class-code-totals" },
      { label: "Return To Main Page", href: "/reports/fis-report", badge: "Menu" },
    ],
    reportKey: "class-code",
  },
  "contract-history": {
    title: "Contract History",
    description: "Search the legacy per-vehicle contract history report.",
    reportKey: "contract-history",
    fields: SEARCH_FIELDS,
  },
  contracts: {
    title: "Contract Reports",
    description: "Legacy contracts report menu with compatible report results.",
    menu: REPORT_MENU_ENTRIES.contracts,
    resolveReportKey: (query) => queryValue(query, "rtype") || "contracts",
  },
  "departments-sites": {
    title: "Department & Site Reports",
    description: "Legacy department and site report menu.",
    menu: REPORT_MENU_ENTRIES.departments,
    resolveReportKey: (query) =>
      ({
        "one-department": "departments-one-department",
        "one-site": "departments-one-site",
        "all-dept-site-count": "departments-sites",
        "all-dept-site-contact": "departments-sites-contact",
        "vehicles-on-els": "departments-vehicles-els",
        "vehicles-on-manual-logs": "departments-vehicles-manual-logs",
        "outstanding-logs-combined": "departments-outstanding-logs-combined",
      })[queryValue(query, "rtype")] ?? "departments-sites",
  },
  "high-distance-all": {
    title: "High Distance Vehicles (All Departments)",
    description: "Legacy all-departments high-distance report.",
    reportKey: "high-distance-all",
    autoLoad: true,
  },
  "high-distance-dept": {
    title: "High Distance Vehicles (Department)",
    description: "Legacy department high-distance report.",
    reportKey: "high-distance-dept",
    autoLoad: true,
  },
  "incorrect-quantities": {
    title: "Incorrect Calculated Quantities",
    description: "Legacy generated report for incorrect calculated vehicle quantities.",
    reportKey: "incorrect-quantities",
    autoLoad: true,
  },
  licences: {
    title: "Licence Reports",
    description: "Legacy licence report menu with compatible licence data.",
    menu: REPORT_MENU_ENTRIES.licences,
    resolveReportKey: (query) =>
      ({
        "gg-number": "licences-gg-number",
        "prov-reg-number": "licences-prov-reg-number",
        "register-number": "licences-register-number",
        "engine-number": "licences-engine-number",
        "chassis-number": "licences-chassis-number",
        site: "licences-site",
        "all-vehicles": "licences",
        "dept-sites-period": "licences-dept-sites-period",
        "expire-date": "licences-expire-date",
        "month-fees": "licences-month-fees",
        "old-expire-dates": "licences-old-expire-dates",
        "sap-info": "licences-sap-info",
        "cof-info": "licences-cof-info",
        "make-model-fee": "licences-make-model-fee",
        "all-with-model-tare-fee": "licences-all-with-model-tare-fee",
        "data-workgroup": "licences-data-workgroup",
        "data-workgroup-latest": "licences-data-workgroup-latest",
        "received-by-ggmt": "licences-received-by-ggmt",
      })[queryValue(query, "rtype")] ?? "licences",
  },
  logbooks: {
    title: "Logbook Reports",
    description: "Legacy logbook report menu.",
    menu: REPORT_MENU_ENTRIES.logbooks,
    resolveReportKey: (query) =>
      ({
        "one-vehicle": "logbooks-one-vehicle",
        "logbook-number": "logbooks-number",
        "dept-site-period": "logbooks",
      })[queryValue(query, "rtype")] ?? "logbooks",
  },
  logsheets: {
    title: "Log Sheet Reports",
    description: "Legacy log sheet report menu.",
    menu: REPORT_MENU_ENTRIES.logsheets,
    resolveReportKey: (query) =>
      ({
        "one-vehicle": "logsheets-one-vehicle",
        "vehicle-odo-balance": "logsheets-vehicle-odo-balance",
        "vehicle-contract-els-manual": "vehicle-logs-report",
        "vehicle-details-per-rek": "logsheets-vehicle-details-per-rek",
        "all-outstanding": "logsheets-all-outstanding",
        "per-dept-site": "logsheets",
      })[queryValue(query, "rtype")] ?? "logsheets",
  },
  management: {
    title: "Management Reports",
    description: "Legacy management report menu.",
    menu: [
      { label: "1) GGMT Management Reports", key: "ggmt-management" },
      { label: "2) Report On Incorrect Captured Data", key: "incorrect-captured-data" },
      { label: "3) FIS Site Management Information", key: "fis-site-management-info" },
    ],
    resolveReportKey: (query) =>
      ({
        "ggmt-management": "management-ggmt",
        "incorrect-captured-data": "management-incorrect-captured-data",
        "fis-site-management-info": "management-site-info",
      })[queryValue(query, "rtype")] ?? "management",
  },
  manuals: {
    title: "Manuals",
    description: "Legacy manuals menu and document targets.",
    menu: [
      { label: "1) General Information", href: "/manuals/RPT_underdev.aspx", badge: "Manual" },
      { label: "2) Accident Manual", href: "/Accident/Doc/Doc_Accidents.htm", badge: "Manual" },
      { label: "3) Auction Manual", href: "/Auction/Doc/Doc_Auctions.htm", badge: "Manual" },
      {
        label: "4) Call Centre Manual",
        href: "/CallCentre/Doc/Doc_CallCentre.htm",
        badge: "Manual",
      },
      {
        label: "5) Contract Manual",
        href: "/contracts/Docs/User Documentation for Contracts Module.html",
        badge: "Manual",
      },
      {
        label: "6) Electronic Log Sheet and Trip Authority Training Manual",
        href: "/Docs/Electronic Log Sheet and Trip Authority Training Manual.doc",
        badge: "Manual",
      },
      { label: "7) Financial Manual", href: "/manuals/RPT_underdev.aspx", badge: "Manual" },
      { label: "8) Fines Manual", href: "/Fines/Doc/Doc_Fines.htm", badge: "Manual" },
      { label: "9) Fuelcard Manual", href: "/Fuelcard/Doc/Doc_Fuelcards.htm", badge: "Manual" },
      { label: "10) Licence Manual", href: "/License/Doc/DOC_LICENCE.htm", badge: "Manual" },
      { label: "11) Logbook Manual", href: "/Logbook/Doc/Doc_Logbooks.htm", badge: "Manual" },
      { label: "12) Logsheet Manual", href: "/Logs/Doc/Doc_Logsheets.htm", badge: "Manual" },
      { label: "13) Losses Manual", href: "/Losses/Doc/Doc_Losses.htm", badge: "Manual" },
      { label: "15) Reports Manual", href: "/Doc/Doc_Reports.htm", badge: "Manual" },
      { label: "16) Taxis Manual", href: "/legacy/taxis/Doc_taxis.htm", badge: "Manual" },
      {
        label: "18) Updating Trip Authorities Manual",
        href: "/Docs/Doc/Updating Trip Authorities Manual2.htm",
        badge: "Manual",
      },
      {
        label: "19) Troubleshoot Manual",
        href: "/TS_Log/Doc/Doc_Troubleshoot.htm",
        badge: "Manual",
      },
      { label: "20) User Admin Manual", href: "/Doc/Doc_UserAdmin.htm", badge: "Manual" },
      {
        label: "21) Validation Data Manual",
        href: "/Validation/Doc/Doc_ValidationData.htm",
        badge: "Manual",
      },
    ],
  },
  "previous-fin-year": {
    title: "Previous Financial Year Reports",
    description: "Legacy previous financial-year report selection.",
    menu: [
      {
        label: "1) Previous Fin Year Manual Logsheet Kilos Captured in Current Fin Year",
        key: "manual",
      },
      {
        label: "2) Previous Fin Year VIP & Taxi Requisitions Captured in Current Fin Year",
        key: "vip-taxi",
      },
    ],
    resolveReportKey: (query) =>
      queryValue(query, "rtype") === "vip-taxi"
        ? "previous-fin-year-vip-taxi"
        : "previous-fin-year-manual-logs",
  },
  "registration-certificates": {
    title: "Registration Certificates",
    description: "Legacy registration certificate report menu.",
    menu: [
      { label: "1) Registration Certificates for All Vehicles", key: "all-vehicles" },
      { label: "2) Registration Certificate for One Vehicle", key: "one-vehicle" },
    ],
    resolveReportKey: (query) =>
      queryValue(query, "rtype") === "one-vehicle"
        ? "registration-certificate-one-vehicle"
        : "registration-certificates",
  },
  tariffs: {
    title: "Tariff Reports",
    description: "Legacy tariff report menu.",
    menu: REPORT_MENU_ENTRIES.tariffs,
    resolveReportKey: (query) =>
      ({
        "class-codes-with-tariffs": "tariffs-class-codes",
        "licence-fees": "tariffs-licence-fees",
        "make-model-with-tariffs": "tariffs-make-model",
        "private-taxi-tariffs": "tariffs-private-taxi",
        "nom-vehicles-without-tariffs": "nom-vehicles-without-tariff",
      })[queryValue(query, "rtype")] ?? "tariffs-fin-year",
  },
  "tariffs-class-2007": {
    title: "Published Tariffs (2007 and Earlier)",
    description: "Legacy tariff report flow for 2007 and earlier.",
    reportKey: "tariffs-class-2007",
    autoLoad: true,
  },
  "tariffs-fin-year": {
    title: "Published Tariffs in a Financial Year",
    description: "Legacy financial-year tariff report flow.",
    reportKey: "tariffs-fin-year",
    fields: DATE_FIELDS,
  },
  "tariffs-per-vehicle": {
    title: "Tariffs per Vehicle",
    description: "Legacy tariffs-per-vehicle report.",
    reportKey: "tariffs-per-vehicle",
    autoLoad: true,
  },
  "trip-authority": {
    title: "Trip Authority Reports",
    description: "Legacy trip authority report menu with compatible report results.",
    menu: [
      { label: "1.1) Registered Vehicles in Site", key: "vehicles-per-site" },
      { label: "1.2) Registered Vehicles in Department", key: "vehicles-per-department" },
      { label: "1.6) Available Vehicles in Site", key: "available-vehicles-site" },
      { label: "2.1) Registered Users in Site", key: "users-per-site" },
      { label: "2.2) Registered Users in Department", key: "users-per-department" },
      { label: "2.3) Registered Users in all Departments", key: "users-all-departments" },
      { label: "3.1) Trips Issued for a Vehicle", key: "trips-for-vehicle" },
      { label: "3.2) All Drivers For a Vehicle", key: "drivers-for-vehicle" },
      { label: "3.3) Vehicle Utilisation by a Driver", key: "vehicle-utilisation-driver" },
      { label: "3.4) Vehicle Utilisation", key: "vehicle-utilisation" },
      { label: "3.5) Vehicles with no trips (Any Department)", key: "vehicles-no-trips" },
      { label: "3.7) Vehicles with high distances in Department", key: "high-distance-department" },
      { label: "3.8) Vehicles with high distances in all departments", key: "high-distance-all" },
      { label: "3.10) Trips open for over 31 days", key: "trips-open-over-31" },
      { label: "4.1) Electronic and Manual Logsheet kilo Report", key: "els-manual-kilo" },
      {
        label: "5.1) Driver Information over Financial Year Selection",
        key: "driver-information-finyear",
      },
    ],
    resolveReportKey: (query) =>
      ({
        "trips-open-over-31": "trips-open-31",
        "high-distance-department": "high-distance-dept",
        "high-distance-all": "high-distance-all",
        "driver-information-finyear": "driver-information-finyear",
      })[queryValue(query, "rtype")] ?? "trip-authority",
  },
  "unallocated-vehicles": {
    title: "Unallocated Vehicles",
    description: "Legacy unallocated vehicle report.",
    reportKey: "unallocated-vehicles",
    autoLoad: true,
  },
  "vehicle-additions": {
    title: "Vehicle Additions",
    description: "Legacy date-range additions flow.",
    reportKey: "vehicle-additions",
    fields: DATE_FIELDS,
  },
  "vehicle-disposals": {
    title: "Vehicle Disposals",
    description: "Legacy date-range disposals flow.",
    reportKey: "vehicle-disposals",
    fields: DATE_FIELDS,
  },
  "vehicle-info": {
    title: "General Vehicle Information Report",
    description: "Legacy vehicle information search flow.",
    reportKey: "vehicle-info",
    fields: SEARCH_FIELDS,
  },
  "vehicle-list-date-range": {
    title: "Vehicle List in a Date Range",
    description: "Legacy vehicle date-range report flow.",
    reportKey: "vehicle-list-date-range",
    fields: DATE_FIELDS,
  },
  "vehicle-logs-report": {
    title: "Vehicle Report - Contracts, ELS & Logsheets",
    description: "Legacy per-vehicle logs report flow.",
    reportKey: "vehicle-logs-report",
    fields: SEARCH_FIELDS,
  },
  "vehicle-status-all": {
    title: "All Vehicle Status",
    description: "Legacy generated report for all vehicle statuses.",
    reportKey: "vehicle-status-all",
    autoLoad: true,
  },
  "vehicle-status-range": {
    title: "Vehicle Status in a Date Range",
    description: "Legacy vehicle status filter flow.",
    reportKey: "vehicle-status-range",
    fields: [
      { name: "status", label: "Vehicle Status", placeholder: "All statuses" },
      ...DATE_FIELDS,
    ],
  },
  "capture-activity": {
    title: "Capture Activity Report",
    description: "Legacy-style capture activity filter flow.",
    reportKey: "capture-activity",
    fields: [
      ...DATE_FIELDS,
      {
        name: "module",
        label: "Module",
        options: [
          { value: "All", label: "All" },
          { value: "Vehicles", label: "Vehicles" },
          { value: "Contracts", label: "Contracts" },
          { value: "Accidents", label: "Accidents" },
          { value: "Fines", label: "Fines" },
          { value: "JobCards", label: "Job Cards" },
          { value: "Logbooks", label: "Logbooks" },
          { value: "Trips", label: "Trips" },
          { value: "Taxis", label: "Taxis" },
          { value: "FuelCards", label: "Fuel Cards" },
        ],
      },
      { name: "site", label: "Site", placeholder: "All sites" },
      { name: "vehicle", label: "GG Number or VMF", placeholder: "GG123 or 14028" },
      { name: "capturedby", label: "Captured By", placeholder: "User code" },
    ],
  },
  users: {
    title: "User Reports",
    description: "Legacy user report menu.",
    menu: [
      { label: "1) Information On One User", key: "one-user" },
      { label: "2) Information On All Users", key: "all-users" },
      { label: "3) Information On All Users Accounts Added On The System", key: "users-added" },
    ],
    resolveReportKey: (query) =>
      ({ "one-user": "users-one", "all-users": "users", "users-added": "users-added" })[
        queryValue(query, "rtype")
      ] ?? "users",
  },
  "vip-pool-utilization-current": {
    title: "VIP and Pool Utilization (Current Month)",
    description: "Legacy generated utilization report.",
    reportKey: "vip-pool-utilization-current",
    autoLoad: true,
  },
  "vip-pool-utilization-previous": {
    title: "VIP and Pool Utilization (Previous Months)",
    description: "Legacy generated utilization report.",
    reportKey: "vip-pool-utilization-previous",
    autoLoad: true,
  },
  "vip-pool-income-current": {
    title: "VIP and Pool Utilization (Current Month, Including Income)",
    description: "Legacy generated utilization and income report.",
    reportKey: "vip-pool-income-current",
    autoLoad: true,
  },
  "vip-pool-income-previous": {
    title: "VIP and Pool Utilization (Previous Months, Including Income)",
    description: "Legacy generated utilization and income report.",
    reportKey: "vip-pool-income-previous",
    autoLoad: true,
  },
  vehicles: {
    title: "Vehicle Reports",
    description: "Legacy vehicle report menu.",
    menu: REPORT_MENU_ENTRIES.vehicles,
    resolveReportKey: (query) =>
      ({
        "vehicle-by-number": "vehicles",
        "vehicle-by-barcode": "vehicle-by-barcode",
        "all-vehicles": "vehicles",
        "vehicles-with-history": "vehicles-with-history",
        "selected-vehicles": "vehicles-selected",
        "older-than-5y-over-120k": "vehicles-older-than-5y-over-120k",
        "older-than-5y-over-120k-period": "vehicles-older-than-5y-over-120k-period",
        "provincial-numbers": "vehicles-provincial-numbers",
        "value-inservice": "vehicles-value-inservice",
        "inservice-per-gg": "vehicles-inservice-per-gg",
        "inservice-per-dept": "vehicles-inservice-per-dept",
        "inservice-wesbank": "vehicles-inservice-wesbank",
        "all-with-barcodes": "vehicles-with-barcodes",
        "replaced-per-dept": "vehicles-replaced-per-dept",
        "lpg-converted": "vehicles-lpg-converted",
        "extended-service": "vehicles-extended-service",
        "vehicle-extras": "vehicles-extras",
        "vehicles-els-manual": "vehicles-els-manual",
        "universal-selected": "vehicles-universal-selected",
      })[queryValue(query, "rtype")] ?? "vehicles",
  },
  "vehicles-no-tariff": {
    title: "Vehicles with Expired or No Tariffs",
    description: "Legacy no/expired tariff report.",
    reportKey: "vehicles-no-tariff",
    autoLoad: true,
  },
  wesbank: {
    title: "Wesbank Transaction Reports",
    description: "Legacy Wesbank transaction report menu.",
    menu: REPORT_MENU_ENTRIES.wesbank,
    resolveReportKey: (query) =>
      ({
        "one-vehicle": "wesbank-one-vehicle",
        "one-vehicle-period": "wesbank-one-vehicle-period",
        "one-dept-site": "wesbank-one-dept-site",
        overfills: "wesbank-overfills",
        "multiple-daily-fuels": "wesbank-multiple-daily-fuels",
      })[queryValue(query, "rtype")] ?? "wesbank",
  },
};

const FIS_REPORT_MENU: readonly ReportMenuEntry[] = [
  {
    label: "1) General Vehicle Information Report (New!!!)",
    href: "/reports/vehicle-info",
    badge: "Menu",
  },
  { label: "2) Accidents Reports", href: "/accidents/reports", badge: "Menu" },
  { label: "4) Auction Reports", href: "/reports/auction", badge: "Menu" },
  { label: "5) Call Centre Reports", href: "/call-centre/reports", badge: "Menu" },
  { label: "6) Class Code Reports", href: "/reports/class-code", badge: "Menu" },
  { label: "7) Contract Reports", href: "/reports/contracts", badge: "Menu" },
  { label: "8) Department & Site Reports", href: "/reports/departments-sites", badge: "Menu" },
  { label: "9) Fines Reports", href: "/reports/fines", badge: "Menu" },
  { label: "10) Fuel Card Reports", href: "/reports/fuel-cards", badge: "Menu" },
  { label: "11) Licence Reports", href: "/reports/licences", badge: "Menu" },
  { label: "12) Logbook Reports", href: "/reports/logbooks", badge: "Menu" },
  { label: "13) Log Sheet Reports", href: "/reports/logsheets", badge: "Menu" },
  { label: "14) Losses Reports", href: "/reports/losses", badge: "Menu" },
  { label: "15) Manuals", href: "/reports/manuals", badge: "Menu" },
  { label: "16) Road Side Assistance", href: "/towing/reports", badge: "Menu" },
  { label: "17) Tariffs", href: "/reports/tariffs", badge: "Menu" },
  { label: "18) Tracking (MobiTrax)", href: "/tracking/reports", badge: "Menu" },
  { label: "19) Taxis", href: "/taxis/reports", badge: "Menu" },
  { label: "20) User Reports", href: "/reports/users", badge: "Menu" },
  { label: "21) Vehicle Reports", href: "/reports/vehicles", badge: "Menu" },
  { label: "22) Wesbank First Auto Reports", href: "/reports/wesbank", badge: "Menu" },
  { label: "23) Workshop Reports", href: "/reports/workshop", badge: "Menu" },
  {
    label: "24) Registration Certificates",
    href: "/reports/registration-certificates",
    badge: "Menu",
  },
  { label: "25) Clearance Reports", href: "/reports/clearance", badge: "Menu" },
  { label: "29) Audit Trail Reports", href: "/reports/audit-trail", badge: "Menu" },
  {
    label: "Request additional reports from the developer",
    href: "/reports/request",
    badge: "Menu",
  },
];

function definitionFor(slug: string): ReportDefinition | null {
  if (slug === "fis-report")
    return {
      title: "FIS Report Main Menu",
      description: "Legacy FIS report main menu.",
      menu: FIS_REPORT_MENU,
    };
  return REPORT_DEFINITIONS[slug] ?? null;
}

function filterQuery(query: ReportQuery) {
  const filters: Record<string, string> = {};
  for (const [key, value] of Object.entries(query)) {
    if (key !== "view" && value !== undefined) {
      const text = Array.isArray(value) ? value[0] : value;
      if (text?.trim()) filters[key] = text;
    }
  }
  return filters;
}

const ReportsRoutePageContent = renderReportsRoutePageContent;

async function renderReportsRoutePageContent({
  slug,
  searchParams,
  forcedReportKey,
}: ReportRouteProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <ReportsFrame title="Reports" description="The reports workspace is temporarily unavailable.">
        <ReportsUnavailable />
      </ReportsFrame>
    );
  if (!hasReportsRole(session.roles))
    return (
      <ReportsFrame title="Reports" description="Legacy report access is enforced on the server.">
        <AccessRestricted />
      </ReportsFrame>
    );

  const query = await searchParams;
  const definition = definitionFor(slug);
  if (!definition)
    return (
      <ReportsFrame
        title="Report not found"
        description="This report route is not part of the migrated report catalog."
      >
        <ReportsUnavailable message="The requested report route is not mapped." />
      </ReportsFrame>
    );

  const reportKey =
    forcedReportKey ?? definition.resolveReportKey?.(query) ?? definition.reportKey ?? "";
  const showingResult =
    forcedReportKey !== undefined || queryValue(query, "view").toLowerCase() === "report";
  const showFilter = !showingResult && definition.fields;

  if (!showingResult && definition.menu) {
    return (
      <ReportsFrame title={definition.title} description={definition.description}>
        <ReportMenu slug={slug} entries={definition.menu}>
          <div className="vehicle-footer-actions">
            <LinkBack href="/reports/fis-report" label="FIS Report Menu" />
            <LinkBack href="/reports" label="Reports Menu" />
          </div>
        </ReportMenu>
      </ReportsFrame>
    );
  }

  if (showFilter) {
    return (
      <ReportsFrame title={definition.title} description={definition.description}>
        <ReportFilterForm
          slug={slug}
          reportKey={reportKey}
          fields={definition.fields ?? []}
          query={query}
        />
      </ReportsFrame>
    );
  }

  let report;
  try {
    report = await getLegacyReport(reportKey, filterQuery(query));
  } catch (error) {
    const message =
      error instanceof LegacyReportApiError && error.reason === "invalid-response"
        ? error.message
        : undefined;
    return (
      <ReportsFrame title={definition.title} description={definition.description}>
        <ReportsUnavailable message={message} />
      </ReportsFrame>
    );
  }

  return (
    <ReportsFrame title={definition.title} description={definition.description}>
      <ReportResult report={report} backHref={definition.menu ? `/reports/${slug}` : "/reports"} />
    </ReportsFrame>
  );
}

export function ReportsRoutePage(props: ReportRouteProps) {
  return (
    <StreamedRoute>
      <ReportsRoutePageContent {...props} />
    </StreamedRoute>
  );
}

function LinkBack({ href, label }: Readonly<{ href: string; label: string }>) {
  return (
    <Link href={href} className="button button-secondary">
      {label}
    </Link>
  );
}

async function ReportsSlugPageContent({
  params,
  searchParams,
}: Readonly<{ params: Promise<{ slug: string }>; searchParams: Promise<ReportQuery> }>) {
  const { slug } = await params;
  return <ReportsRoutePageContent slug={slug.toLowerCase()} searchParams={searchParams} />;
}

export default function ReportsSlugPage(
  props: Readonly<{
    params: Promise<{ slug: string }>;
    searchParams: Promise<ReportQuery>;
  }>,
) {
  return (
    <StreamedRoute>
      <ReportsSlugPageContent {...props} />
    </StreamedRoute>
  );
}
