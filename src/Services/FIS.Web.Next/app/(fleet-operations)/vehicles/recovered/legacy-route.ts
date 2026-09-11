import { createElement } from "react";

import RecoveredVehiclePage from "./_route";
import type { RecoveredVehicleRoutePath } from "./_route";

type SearchParams = Promise<{
  GGnum?: string | string[];
  mode?: string | string[];
  search?: string | string[];
}>;
type LegacyRecoveredVehiclePageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyRecoveredVehiclePage(routePath: RecoveredVehicleRoutePath) {
  return function LegacyRecoveredVehiclePage({ searchParams }: LegacyRecoveredVehiclePageProps) {
    return createElement(RecoveredVehiclePage, { routePath, searchParams });
  };
}
