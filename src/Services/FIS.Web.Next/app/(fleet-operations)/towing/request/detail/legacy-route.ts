import { createElement } from "react";

import TowingDetailPage from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type LegacyTowingDetailPageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyTowingDetailPage(routePath: string) {
  return function LegacyTowingDetailPage({ searchParams }: LegacyTowingDetailPageProps) {
    return createElement(TowingDetailPage, { routePath, searchParams });
  };
}
