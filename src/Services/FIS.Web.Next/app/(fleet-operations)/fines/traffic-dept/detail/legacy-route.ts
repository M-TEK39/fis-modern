import { createElement } from "react";

import TrafficDeptDetailPage from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type LegacyTrafficDeptDetailPageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyTrafficDeptDetailPage(routePath: string) {
  return function LegacyTrafficDeptDetailPage({ searchParams }: LegacyTrafficDeptDetailPageProps) {
    return createElement(TrafficDeptDetailPage, { routePath, searchParams });
  };
}
