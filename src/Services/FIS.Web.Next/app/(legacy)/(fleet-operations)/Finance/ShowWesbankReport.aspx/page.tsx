import { redirect } from "next/navigation";

import { StreamedRoute } from "@/components/app-shell/streamed-route";

import { legacyFinanceDrillDownPath, type LegacyFinanceQuery } from "../legacy-finance-redirect";

async function LegacyWesbankDetailContent({
  searchParams,
}: Readonly<{ searchParams: Promise<LegacyFinanceQuery> }>): Promise<never> {
  redirect(legacyFinanceDrillDownPath(await searchParams, "wesbank"));
}

export default function LegacyWesbankDetailPage(
  props: Readonly<{ searchParams: Promise<LegacyFinanceQuery> }>,
) {
  return (
    <StreamedRoute>
      <LegacyWesbankDetailContent {...props} />
    </StreamedRoute>
  );
}
