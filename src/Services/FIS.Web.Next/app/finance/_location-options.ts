import type { DepartmentRecord } from "@/lib/api-departments";
import type { SiteRecord } from "@/lib/api-sites";

export type FinanceLocationOption = {
  value: string;
  label: string;
};

export function departmentOptions(
  departments: readonly Pick<DepartmentRecord, "departmentCode" | "description">[],
): FinanceLocationOption[] {
  return departments.map((department) => ({
    value: String(department.departmentCode),
    label: `${department.description || "Unnamed department"} (${department.departmentCode})`,
  }));
}

export function siteOptions(
  sites: readonly Pick<SiteRecord, "siteCode" | "description">[],
): FinanceLocationOption[] {
  return sites.map((site) => ({
    value: String(site.siteCode),
    label: `${site.description || "Unnamed site"} (${site.siteCode})`,
  }));
}
