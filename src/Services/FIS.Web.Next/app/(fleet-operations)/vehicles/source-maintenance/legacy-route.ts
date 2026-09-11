import { createElement } from "react";

import VehicleSourcePage from "./_route";

type SearchParams = Promise<{ saved?: string | string[] }>;
type VehicleSourceLegacyRoutePath =
  | "/vehicles/source-maintenance"
  | "/Master-File/Vehicle_Source.aspx"
  | "/Master-File/Add_Vehicle_Source.aspx"
  | "/Master-File/Edit_Vehicle_Source.aspx"
  | "/Master-File/Edit_Vehicle_Source_2.aspx";
type LegacyVehicleSourcePageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyVehicleSourcePage(routePath: VehicleSourceLegacyRoutePath) {
  return function LegacyVehicleSourcePage({ searchParams }: LegacyVehicleSourcePageProps) {
    return createElement(VehicleSourcePage, { routePath, searchParams });
  };
}
