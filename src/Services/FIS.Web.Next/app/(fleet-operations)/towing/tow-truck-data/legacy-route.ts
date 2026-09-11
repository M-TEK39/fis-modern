import { createElement } from "react";

import TowTruckDataPage from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type LegacyTowTruckDataPageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyTowTruckDataPage(routePath: string) {
  return function LegacyTowTruckDataPage({ searchParams }: LegacyTowTruckDataPageProps) {
    return createElement(TowTruckDataPage, { routePath, searchParams });
  };
}
