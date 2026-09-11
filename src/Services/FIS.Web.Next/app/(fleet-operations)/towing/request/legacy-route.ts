import { createElement } from "react";

import TowingRequestPage from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type LegacyTowingRequestPageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyTowingRequestPage(routePath: string) {
  return function LegacyTowingRequestPage({ searchParams }: LegacyTowingRequestPageProps) {
    return createElement(TowingRequestPage, { routePath, searchParams });
  };
}
