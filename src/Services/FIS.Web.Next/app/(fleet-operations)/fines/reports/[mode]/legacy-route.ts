import { createElement } from "react";

import { FineReportPage } from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type LegacyFineReportPageProps = Readonly<{ searchParams: SearchParams }>;
type LegacyFineReportConfig = Readonly<{
  forcedMode: string;
  routePath: string;
  legacyResult?: boolean;
}>;

export function createLegacyFineReportPage({
  forcedMode,
  routePath,
  legacyResult = false,
}: LegacyFineReportConfig) {
  return function LegacyFineReportPage({ searchParams }: LegacyFineReportPageProps) {
    return createElement(FineReportPage, {
      forcedMode,
      legacyResult,
      routePath,
      searchParams,
    });
  };
}
