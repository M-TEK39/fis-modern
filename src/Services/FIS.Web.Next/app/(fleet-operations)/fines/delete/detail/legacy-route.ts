import { createElement } from "react";

import FineDeleteDetailPage from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type LegacyFineDeleteDetailPageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyFineDeleteDetailPage(routePath: string) {
  return function LegacyFineDeleteDetailPage({ searchParams }: LegacyFineDeleteDetailPageProps) {
    return createElement(FineDeleteDetailPage, { routePath, searchParams });
  };
}
