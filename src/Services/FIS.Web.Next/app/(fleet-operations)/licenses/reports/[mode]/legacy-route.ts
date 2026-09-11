import { createElement } from "react";

import LicenseReportPage from "./_route";
import type { LicenseReportMode } from "@/lib/api/reports/api-license-reports";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type LegacyLicenseReportPageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyLicenseReportPage(forcedMode: LicenseReportMode) {
  return function LegacyLicenseReportPage({ searchParams }: LegacyLicenseReportPageProps) {
    return createElement(LicenseReportPage, {
      forcedMode,
      params: Promise.resolve({ mode: forcedMode }),
      searchParams,
    });
  };
}
