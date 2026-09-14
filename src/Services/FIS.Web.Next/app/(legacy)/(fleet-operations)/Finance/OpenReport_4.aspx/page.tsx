import { redirect } from "next/navigation";

import { StreamedRoute } from "@/components/app-shell/streamed-route";

import {
  pathWithLegacyQuery,
  type LegacyFinanceQuery,
} from "../legacy-finance-redirect";

async function LegacyFinanceOpenReport4Content({
  searchParams,
}: Readonly<{ searchParams: Promise<LegacyFinanceQuery> }>): Promise<never> {
  redirect(pathWithLegacyQuery("/FISReports/OpenReport_4.aspx", await searchParams));
}

export default function LegacyFinanceOpenReport4Page(
  props: Readonly<{ searchParams: Promise<LegacyFinanceQuery> }>,
) {
  return (
    <StreamedRoute>
      <LegacyFinanceOpenReport4Content {...props} />
    </StreamedRoute>
  );
}
