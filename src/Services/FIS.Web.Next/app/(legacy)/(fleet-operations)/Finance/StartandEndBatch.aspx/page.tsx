import { redirect } from "next/navigation";

import { StreamedRoute } from "@/components/app-shell/streamed-route";

import {
  batchManagementPath,
  type LegacyFinanceQuery,
} from "../legacy-finance-redirect";

async function LegacyStartOrFinishBatchContent({
  searchParams,
}: Readonly<{ searchParams: Promise<LegacyFinanceQuery> }>): Promise<never> {
  redirect(batchManagementPath(await searchParams));
}

export default function LegacyStartOrFinishBatchPage(
  props: Readonly<{ searchParams: Promise<LegacyFinanceQuery> }>,
) {
  return (
    <StreamedRoute>
      <LegacyStartOrFinishBatchContent {...props} />
    </StreamedRoute>
  );
}
