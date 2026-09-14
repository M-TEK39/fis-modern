import { redirect } from "next/navigation";

import { StreamedRoute } from "@/components/app-shell/streamed-route";

import { legacyFinanceDrillDownPath, type LegacyFinanceQuery } from "../legacy-finance-redirect";

async function LegacyTotalCostDetailContent({
  searchParams,
}: Readonly<{ searchParams: Promise<LegacyFinanceQuery> }>): Promise<never> {
  redirect(legacyFinanceDrillDownPath(await searchParams, "regional"));
}

export default function LegacyTotalCostDetailPage(
  props: Readonly<{ searchParams: Promise<LegacyFinanceQuery> }>,
) {
  return (
    <StreamedRoute>
      <LegacyTotalCostDetailContent {...props} />
    </StreamedRoute>
  );
}
