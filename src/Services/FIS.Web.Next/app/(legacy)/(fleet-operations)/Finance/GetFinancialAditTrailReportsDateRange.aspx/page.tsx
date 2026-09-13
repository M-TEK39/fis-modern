import { redirect } from "next/navigation";

import { StreamedRoute } from "@/components/app-shell/streamed-route";

import {
  auditTrailPath,
  type LegacyFinanceQuery,
} from "../legacy-finance-redirect";

async function LegacyFinanceAuditTrailParametersContent({
  searchParams,
}: Readonly<{ searchParams: Promise<LegacyFinanceQuery> }>): Promise<never> {
  redirect(auditTrailPath(await searchParams));
}

export default function LegacyFinanceAuditTrailParametersPage(
  props: Readonly<{ searchParams: Promise<LegacyFinanceQuery> }>,
) {
  return (
    <StreamedRoute>
      <LegacyFinanceAuditTrailParametersContent {...props} />
    </StreamedRoute>
  );
}
