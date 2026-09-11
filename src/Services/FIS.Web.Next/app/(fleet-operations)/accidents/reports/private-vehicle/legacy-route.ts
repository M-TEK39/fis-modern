import { createElement } from "react";

import { PrivateVehicleReportPage } from "./_route";
import type { AccidentPrivateVehicleReportMode } from "@/lib/api/fleet-operations/api-accidents";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type LegacyPrivateVehicleReportPageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyPrivateVehicleReportPage(
  defaultMode: AccidentPrivateVehicleReportMode = "third-party",
) {
  return function LegacyPrivateVehicleReportPage({
    searchParams,
  }: LegacyPrivateVehicleReportPageProps) {
    return createElement(PrivateVehicleReportPage, { defaultMode, searchParams });
  };
}
