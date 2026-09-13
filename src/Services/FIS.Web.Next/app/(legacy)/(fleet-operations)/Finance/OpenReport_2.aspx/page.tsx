import { redirect } from "next/navigation";

import { StreamedRoute } from "@/components/app-shell/streamed-route";

import {
  pathWithLegacyQuery,
  type LegacyFinanceQuery,
} from "../legacy-finance-redirect";

async function LegacyFinanceOpenReport2Content({
  searchParams,
}: Readonly<{ searchParams: Promise<LegacyFinanceQuery> }>): Promise<never> {
  // OpenReport_2 and OpenReport share the same request contract for the
  // Finance report types the modern report-output route supports.
  redirect(pathWithLegacyQuery("/Finance/OpenReport.aspx", await searchParams));
}

export default function LegacyFinanceOpenReport2Page(
  props: Readonly<{ searchParams: Promise<LegacyFinanceQuery> }>,
) {
  return (
    <StreamedRoute>
      <LegacyFinanceOpenReport2Content {...props} />
    </StreamedRoute>
  );
}
