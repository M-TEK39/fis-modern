import WesbankReportPage from "@/app/(fleet-operations)/finance/wesbank/[action]/page";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type Query = Record<string, string | string[] | undefined>;

export default function LegacyWesbankProvinceSummaryPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  return (
    <StreamedRoute>
      <WesbankReportPage
        params={Promise.resolve({ action: "summary-selection" })}
        searchParams={searchParams}
      />
    </StreamedRoute>
  );
}
