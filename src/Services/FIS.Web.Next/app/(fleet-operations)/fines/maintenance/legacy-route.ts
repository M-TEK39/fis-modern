import { createElement } from "react";

import FineMaintenancePage from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type LegacyFineMaintenancePageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyFineMaintenancePage(routePath: string) {
  return function LegacyFineMaintenancePage({ searchParams }: LegacyFineMaintenancePageProps) {
    return createElement(FineMaintenancePage, { routePath, searchParams });
  };
}
