import { createElement } from "react";

import FineDeletePage from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type LegacyFineDeletePageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyFineDeletePage(routePath: string) {
  return function LegacyFineDeletePage({ searchParams }: LegacyFineDeletePageProps) {
    return createElement(FineDeletePage, { routePath, searchParams });
  };
}
