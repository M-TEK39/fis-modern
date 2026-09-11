import { createElement } from "react";

import TowingFirmDatePage from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type LegacyTowingFirmDatePageProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyTowingFirmDatePage(routePath: string) {
  return function LegacyTowingFirmDatePage({ searchParams }: LegacyTowingFirmDatePageProps) {
    return createElement(TowingFirmDatePage, { routePath, searchParams });
  };
}
