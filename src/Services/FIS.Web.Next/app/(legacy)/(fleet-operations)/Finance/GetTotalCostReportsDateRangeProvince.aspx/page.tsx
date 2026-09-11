import RegionalFinanceActionPage from "@/app/(fleet-operations)/finance/regional/[action]/page";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type Query = Record<string, string | string[] | undefined>;

export default function LegacyRegionalProvinceReportPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  return (
    <StreamedRoute>
      <RegionalFinanceActionPage
        params={Promise.resolve({ action: "summary-per-province" })}
        searchParams={searchParams}
      />
    </StreamedRoute>
  );
}
