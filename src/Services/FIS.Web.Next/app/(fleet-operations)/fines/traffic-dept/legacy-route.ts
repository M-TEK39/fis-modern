import { createElement } from "react";

import TrafficDeptPage from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type LegacyTrafficDeptPageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyTrafficDeptPage(routePath: string) {
  return function LegacyTrafficDeptPage({ searchParams }: LegacyTrafficDeptPageProps) {
    return createElement(TrafficDeptPage, { routePath, searchParams });
  };
}
