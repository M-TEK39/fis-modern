import { createElement } from "react";

import FineDetailPage from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type LegacyFineDetailPageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyFineDetailPage(routePath: string) {
  return function LegacyFineDetailPage({ searchParams }: LegacyFineDetailPageProps) {
    return createElement(FineDetailPage, { routePath, searchParams });
  };
}
