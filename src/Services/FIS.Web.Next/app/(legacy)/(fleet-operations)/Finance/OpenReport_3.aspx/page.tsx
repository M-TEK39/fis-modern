import { redirect } from "next/navigation";

import { StreamedRoute } from "@/components/app-shell/streamed-route";

import {
  pathWithLegacyQuery,
  type LegacyFinanceQuery,
} from "../legacy-finance-redirect";

async function LegacyFinanceOpenReport3Content({
  searchParams,
}: Readonly<{ searchParams: Promise<LegacyFinanceQuery> }>): Promise<never> {
  redirect(pathWithLegacyQuery("/FISReports/OpenReport_3.aspx", await searchParams));
}

export default function LegacyFinanceOpenReport3Page(
  props: Readonly<{ searchParams: Promise<LegacyFinanceQuery> }>,
) {
  return (
    <StreamedRoute>
      <LegacyFinanceOpenReport3Content {...props} />
    </StreamedRoute>
  );
}
