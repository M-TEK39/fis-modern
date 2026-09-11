import { createElement } from "react";

import TowTruckDetailPage from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type LegacyTowTruckDetailPageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyTowTruckDetailPage(routePath: string) {
  return function LegacyTowTruckDetailPage({ searchParams }: LegacyTowTruckDetailPageProps) {
    return createElement(TowTruckDetailPage, { routePath, searchParams });
  };
}
