import { createElement } from "react";

import TowingRequestReportPage from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type LegacyTowingRequestReportPageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyTowingRequestReportPage(routePath: string) {
  return function LegacyTowingRequestReportPage({
    searchParams,
  }: LegacyTowingRequestReportPageProps) {
    return createElement(TowingRequestReportPage, { routePath, searchParams });
  };
}
