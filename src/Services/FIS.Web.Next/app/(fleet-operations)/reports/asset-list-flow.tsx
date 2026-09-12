import Link from "next/link";

import type { DepartmentRecord } from "@/lib/api/reference-data/api-departments";
import type { SiteRecord } from "@/lib/api/reference-data/api-sites";
import type { FinanceOption } from "@/lib/api/finance/api-finance";

export type AssetListMode = "all-departments" | "by-province" | "by-department" | "by-site";

export const ASSET_LIST_MODE_VALUES: readonly AssetListMode[] = [
  "all-departments",
  "by-province",
  "by-department",
  "by-site",
];

export function normalizeAssetListMode(value: string) {
  const normalized = value.trim().toLowerCase();
  return ASSET_LIST_MODE_VALUES.includes(normalized as AssetListMode)
    ? (normalized as AssetListMode)
    : "all-departments";
}

export function assetListReportKey(mode: AssetListMode) {
  return mode === "all-departments" ? "asset-list" : `asset-list-${mode}`;
}

export function assetListCaption(mode: AssetListMode) {
  switch (mode) {
    case "by-province":
      return "Asset List: All Vehicles in FIS with status New & In-Service (View by PROVINCE)";
    case "by-department":
      return "Asset List: All Vehicles in FIS with status New & In-Service (View by DEPARTMENT)";
    case "by-site":
      return "Asset List: All Vehicles in FIS with status New & In-Service (View by SITE)";
    default:
      return "Asset List: All Vehicles in FIS with status New & In-Service (All Departments)";
  }
}

export function assetListNeedsSelection(mode: AssetListMode) {
  return mode !== "all-departments";
}

type Option = Readonly<{ value: string; label: string }>;

function optionList(options: readonly Option[], emptyLabel: string) {
  return (
    <>
      <option value="">{emptyLabel}</option>
      {options.map((option) => (
        <option key={`${option.value}-${option.label}`} value={option.value}>
          {option.label}
        </option>
      ))}
    </>
  );
}

function departmentOptions(departments: readonly DepartmentRecord[]) {
  return departments
    .slice()
    .sort((left, right) => {
      const leftKey = `${left.departmentNumber ?? ""} ${left.description ?? ""}`;
      const rightKey = `${right.departmentNumber ?? ""} ${right.description ?? ""}`;
      return leftKey.localeCompare(rightKey, undefined, { numeric: true, sensitivity: "base" });
    })
    .map((department) => ({
      value: String(department.departmentCode),
      label: department.description
        ? department.departmentNumber
          ? `${department.departmentNumber} ${department.description}`
          : department.description
        : `Department ${department.departmentCode}`,
    }));
}

function siteOptions(sites: readonly SiteRecord[], departmentCode: string) {
  const selectedDepartment = Number(departmentCode);
  return sites
    .filter((site) => site.departmentCode === selectedDepartment)
    .sort((left, right) =>
      `${left.description ?? ""} ${left.siteCode}`.localeCompare(
        `${right.description ?? ""} ${right.siteCode}`,
        undefined,
        { numeric: true, sensitivity: "base" },
      ),
    )
    .map((site) => ({
      value: String(site.siteCode),
      label: site.description
        ? `${site.departmentNumber ? `${site.departmentNumber} ` : ""}${site.description} (${site.siteCode})`
        : `Site ${site.siteCode}`,
    }));
}

export function AssetListSelector({
  mode,
  selectedProvince,
  selectedDepartment,
  selectedSite,
  departments,
  provinces,
  sites,
  departmentSelectionLocked = false,
  error,
}: Readonly<{
  mode: Exclude<AssetListMode, "all-departments">;
  selectedProvince: string;
  selectedDepartment: string;
  selectedSite: string;
  departments: readonly DepartmentRecord[];
  provinces: readonly FinanceOption[];
  sites: readonly SiteRecord[];
  departmentSelectionLocked?: boolean;
  error?: string;
}>) {
  const departmentOptionsList = departmentOptions(departments);
  const selectedDepartmentRecord = departments.find(
    (department) => String(department.departmentCode) === selectedDepartment,
  );
  const selectedDepartmentLabel = selectedDepartmentRecord
    ? departmentOptions([selectedDepartmentRecord])[0]?.label
    : selectedDepartment
      ? `Department ${selectedDepartment}`
      : "";
  const filteredSiteOptions = siteOptions(sites, selectedDepartment);
  const siteStageReady = mode === "by-site" && selectedDepartment.length > 0;
  const formView = mode === "by-site" && !selectedSite ? "filters" : "report";
  const siteButtonLabel = mode === "by-site" && !siteStageReady ? "Load Sites" : "View Report";
  const noReferenceOptions =
    (mode === "by-province" && provinces.length === 0) ||
    ((mode === "by-department" || mode === "by-site") && departments.length === 0);

  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="asset-list-filter-title">
      <p className="eyebrow">Legacy report filters</p>
      <h2 id="asset-list-filter-title">{assetListCaption(mode)}</h2>
      <p className="muted-copy">
        Select the legacy grouping before loading the asset list. New and In-Service vehicles are
        included in the report.
      </p>
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      {noReferenceOptions && !error ? (
        <div className="vehicle-empty-state" role="status">
          <p>
            No {mode === "by-province" ? "provinces" : mode === "by-site" ? "sites" : "departments"}{" "}
            are available for this report.
          </p>
        </div>
      ) : null}
      <form method="get">
        <input name="rtype" type="hidden" value={mode} />
        <input name="view" type="hidden" value={formView} />
        <div className="form-grid">
          {mode === "by-province" ? (
            <div className="form-field">
              <label className="form-label" htmlFor="asset-list-province">
                Province
              </label>
              <select
                className="form-select"
                id="asset-list-province"
                name="province"
                defaultValue={selectedProvince}
                required
              >
                {optionList(provinces, "Select Province")}
              </select>
            </div>
          ) : null}
          {mode === "by-department" || mode === "by-site" ? (
            <div className="form-field">
              {departmentSelectionLocked ? (
                <>
                  <span className="form-label">Department</span>
                  {selectedDepartmentLabel ? (
                    <p className="form-input" aria-label="Department">
                      {selectedDepartmentLabel}
                    </p>
                  ) : (
                    <p className="form-help">Your department scope is not available.</p>
                  )}
                  {selectedDepartment ? (
                    <input name="department" type="hidden" value={selectedDepartment} />
                  ) : null}
                </>
              ) : (
                <>
                  <label className="form-label" htmlFor="asset-list-department">
                    Department
                  </label>
                  <select
                    className="form-select"
                    id="asset-list-department"
                    name="department"
                    defaultValue={selectedDepartment}
                    required
                  >
                    {optionList(departmentOptionsList, "Select Department")}
                  </select>
                </>
              )}
            </div>
          ) : null}
          {mode === "by-site" && siteStageReady ? (
            <div className="form-field">
              <label className="form-label" htmlFor="asset-list-site">
                Site
              </label>
              <select
                className="form-select"
                id="asset-list-site"
                name="site"
                defaultValue={selectedSite}
                required
                disabled={filteredSiteOptions.length === 0}
              >
                {optionList(filteredSiteOptions, "Select Site")}
              </select>
              {filteredSiteOptions.length === 0 ? (
                <p className="form-help">No sites are available for this department.</p>
              ) : null}
            </div>
          ) : null}
        </div>
        <div className="button-row asset-list-selector-actions">
          <button
            className="button button-primary"
            type="submit"
            disabled={noReferenceOptions && !error}
          >
            {siteButtonLabel}
          </button>
          <Link className="button button-secondary" href="/reports/asset-list">
            Back to Asset List Menu
          </Link>
          <Link className="button button-secondary" href="/reports">
            Reports Menu
          </Link>
        </div>
      </form>
      <div className="notice notice-info" role="note">
        Detailed reports can be large. You may have to wait a few minutes before the report is
        displayed.
      </div>
    </section>
  );
}
