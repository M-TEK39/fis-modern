import { createElement } from "react";

import { HqAccidentRoute } from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type LegacyHqAccidentPageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyHqAccidentPage(locationCode: number) {
  return function LegacyHqAccidentPage({ searchParams }: LegacyHqAccidentPageProps) {
    return createElement(HqAccidentRoute, { searchParams, locationCode });
  };
}
