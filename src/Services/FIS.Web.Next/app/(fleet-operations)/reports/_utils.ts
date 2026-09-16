export type ReportQuery = Record<string, string | string[] | undefined>;

export function hasReportsRole(roles: readonly string[]) {
  return roles.some(
    (role) =>
      role.trim().toLowerCase() === "reports" ||
      role.trim().toLowerCase() === "administrator" ||
      role.trim().toLowerCase() === "admin",
  );
}

export function hasContractReportsRole(roles: readonly string[]) {
  return hasReportsRole(roles) || roles.some((role) => role.trim().toLowerCase() === "contracts");
}

export type ReportMenuEntry = {
  label: string;
  key?: string;
  href?: string;
  badge?: string;
};

export type ReportField = {
  name: string;
  label: string;
  type?: "text" | "date" | "number";
  placeholder?: string;
  options?: readonly { value: string; label: string }[];
};

export function queryValue(query: ReportQuery, key: string) {
  const value = query[key];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

export function hasQueryValue(query: ReportQuery, key: string) {
  return queryValue(query, key).trim().length > 0;
}

export function reportHref(
  slug: string,
  key?: string,
  filters: Record<string, string | undefined> = {},
) {
  const params = new URLSearchParams();
  if (key) params.set("rtype", key);
  params.set("view", "report");
  for (const [name, value] of Object.entries(filters)) {
    if (value?.trim()) params.set(name, value);
  }
  return `/reports/${slug}?${params.toString()}`;
}

export function legacyReportHref(slug: string, key?: string) {
  return reportHref(slug, key);
}

export const REPORT_MENU_ENTRIES = {
  assetList: [
    {
      label: "i) Asset List: All Vehicles in FIS with status New & In-Service (All Departments)",
      key: "all-departments",
    },
    {
      label: "ii) Asset List: All Vehicles in FIS with status New & In-Service (View by PROVINCE)",
      key: "by-province",
    },
    {
      label:
        "iii) Asset List: All Vehicles in FIS with status New & In-Service (View by DEPARTMENT)",
      key: "by-department",
    },
    {
      label: "iv) Asset List: All Vehicles in FIS with status New & In-Service (View by SITE)",
      key: "by-site",
    },
  ],
  auditTrail: [
    { label: "1) Audit Trail grouped by Department in a date range", key: "department" },
    { label: "2) Audit Trail grouped by Site in a date range", key: "site" },
    { label: "3) Audit Trail grouped per Vehicle in a date range", key: "vehicle" },
  ],
  contracts: [
    { label: "1) Information On a Vehicle Contracts", key: "vehicle-contract-single" },
    { label: "2) Information On Multiple Vehicle Contracts", key: "vehicle-contract-multiple" },
    {
      label: "3) Information On All Vehicles Contracts (Universal Report)",
      key: "vehicle-contract-universal",
    },
    {
      label: "4) Information On All Contracts That Will Expire By a Specified Date",
      key: "contracts-expiring-by-date",
    },
    { label: "5) Contract summarized report", key: "contract-summary" },
    { label: "6) Vehicle Trip Authorities per GG or GP Number", key: "trip-authorities-single" },
    {
      label: "7) Multiple Vehicle Trip Authorities per GG or GP Number",
      key: "trip-authorities-multiple",
    },
    {
      label: "8) Vehicle Trip Authorities per Dept, Site or Date",
      key: "trip-authorities-by-dept-site-date",
    },
    { label: "9) Daily & Monthly Contract check list report", key: "contracts-checklist" },
    { label: "10) Vehicle Contracts with NO distance traveled", key: "contracts-no-distance" },
    { label: "11) Contracts for a Department for period", key: "contracts-per-dept-period" },
    { label: "12) Permanent Section - Contracts Fleet Reports", key: "contracts-fleet-reports" },
    {
      label: "Lease Vehicles Split Report for Vehicles not on Contract and those on Contract",
      key: "lease-nom-split",
    },
  ],
  departments: [
    { label: "1) One Department", key: "one-department" },
    { label: "2) One Site", key: "one-site" },
    { label: "3) All Departments & Site with number vehicles", key: "all-dept-site-count" },
    { label: "4) All Departments & Sites with contact details", key: "all-dept-site-contact" },
    { label: "5) List of Vehicles on ELS", key: "vehicles-on-els" },
    { label: "6) List of Vehicles on Manual LOGSHEETS", key: "vehicles-on-manual-logs" },
    {
      label: "7) Outstanding Logs per Dept: ELS and Manual Logs Combined",
      key: "outstanding-logs-combined",
    },
  ],
  licences: [
    { label: "1) Licence Report for a GG Number", key: "gg-number" },
    { label: "2) Licence Report for a Prov Reg Number", key: "prov-reg-number" },
    { label: "3) Licence Report for a Register Number", key: "register-number" },
    { label: "4) Licence Report for an Engine Number", key: "engine-number" },
    { label: "5) Licence Report for a Chassis Number", key: "chassis-number" },
    { label: "6) Licence Report for a Site", key: "site" },
    { label: "7) Licence Report on ALL Vehicles", key: "all-vehicles" },
    { label: "8) Licences, for a Dept, with All Sites, for a period", key: "dept-sites-period" },
    { label: "9) Report on Licence EXPIRE DATE", key: "expire-date" },
    { label: "10) Licences fees for a month", key: "month-fees" },
    {
      label: "11) Licences of In Service Vehicles, with OLD Expire Dates",
      key: "old-expire-dates",
    },
    { label: "12) SAP Information", key: "sap-info" },
    { label: "13) COF Information", key: "cof-info" },
    { label: "14) Table: Make & Model with Licence Fee", key: "make-model-fee" },
    {
      label: "15) All Vehicles with Make & Model & Tare & Licence Fee",
      key: "all-with-model-tare-fee",
    },
    { label: "16) Data Workgroup - Report for Each or Group of Vehicle", key: "data-workgroup" },
    {
      label: "17) Data Workgroup - Only Latest Report for Each or Group of Vehicle",
      key: "data-workgroup-latest",
    },
    { label: "18) Licence Received by GGMT: Receiver Name and Date", key: "received-by-ggmt" },
  ],
  logbooks: [
    { label: "1) Logbook Report on ONE Vehicle", key: "one-vehicle" },
    { label: "2) Logbook Report on Logbook Number", key: "logbook-number" },
    { label: "3) Logbook Report, for a Dept/Site, for a period", key: "dept-site-period" },
  ],
  logsheets: [
    { label: "1) Log Report On One Vehicle", key: "one-vehicle" },
    { label: "2) Report Per Vehicle - Log Odo Balance", key: "vehicle-odo-balance" },
    {
      label: "3) Vehicle report - View: Contracts, ELS & Manual Logs Details in one report",
      key: "vehicle-contract-els-manual",
    },
    { label: "4) Report Vehicle Details Per Rek num.", key: "vehicle-details-per-rek" },
    { label: "5) All outstanding Logsheets", key: "all-outstanding" },
    { label: "6) Logsheet Report per Department or Site", key: "per-dept-site" },
  ],
  tariffs: [
    { label: "1) Class codes with tariffs", key: "class-codes-with-tariffs" },
    { label: "2) Licence fees", key: "licence-fees" },
    { label: "3) Make Model with tariffs", key: "make-model-with-tariffs" },
    { label: "4) Private Taxi Tariffs", key: "private-taxi-tariffs" },
    {
      label: "5) List of NOM Vehicles that do not have Tariffs",
      key: "nom-vehicles-without-tariffs",
    },
  ],
  vehicles: [
    {
      label: "1) Information On A Vehicle BY GG, Registration, Engine or Chassis Number",
      key: "vehicle-by-number",
    },
    { label: "2) Information On A Vehicle BY barcode", key: "vehicle-by-barcode" },
    { label: "3) Information On All Vehicles", key: "all-vehicles" },
    { label: "4) Information On Vehicles With A History", key: "vehicles-with-history" },
    { label: "5) Report For Selected Vehicles", key: "selected-vehicles" },
    {
      label: "6) Vehicles Older Than 5 Years And Done More Than 120000 km's",
      key: "older-than-5y-over-120k",
    },
    {
      label:
        "7) Vehicles Older Than 5 Years And Done More Than 120000 km's WITH km's Travelled for a period",
      key: "older-than-5y-over-120k-period",
    },
    { label: "8) Vehicles With Provincial Numbers", key: "provincial-numbers" },
    { label: "9) Value Of IN SERVICE Vehicles", key: "value-inservice" },
    { label: "10) All IN SERVICE Vehicles per GG Number", key: "inservice-per-gg" },
    { label: "11) All IN SERVICE Vehicles per Department", key: "inservice-per-dept" },
    {
      label: "12) All IN SERVICE Vehicles with Departments or Blank (for Wesbank)",
      key: "inservice-wesbank",
    },
    { label: "13) All vehicles with barcodes", key: "all-with-barcodes" },
    { label: "14) All vehicles replaced/not replaced per Department", key: "replaced-per-dept" },
    { label: "15) All LPG Converted Vehicles", key: "lpg-converted" },
    { label: "16) All Extended Service Vehicles", key: "extended-service" },
    {
      label: "17) All Vehicles fitted with Extras e.g. Tyre Safety Bands, Netstar",
      key: "vehicle-extras",
    },
    { label: "18) Information On Vehicles on ELS or Manual LOGSHEET", key: "vehicles-els-manual" },
    { label: "19) Universal report for selected vehicles", key: "universal-selected" },
  ],
  wesbank: [
    { label: "1) Wesbank Transaction Report for ONE Vehicle", key: "one-vehicle" },
    {
      label: "2) Wesbank Transaction Report for ONE Vehicle, for a Period",
      key: "one-vehicle-period",
    },
    { label: "3) Wesbank Transaction Report for ONE Dept Site", key: "one-dept-site" },
    { label: "4) Exception Report Overfills", key: "overfills", badge: "Exception" },
    {
      label: "5) Exception Multiple Daily Fuel Transactions",
      key: "multiple-daily-fuels",
      badge: "Exception",
    },
  ],
} as const;
