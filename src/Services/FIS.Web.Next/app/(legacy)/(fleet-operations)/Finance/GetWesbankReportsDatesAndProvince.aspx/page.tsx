import WesbankReportPage from "@/app/(fleet-operations)/finance/wesbank/[action]/page";

type Query = Record<string, string | string[] | undefined>;

export default function LegacyWesbankProvinceSummaryPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  return (
    <WesbankReportPage
      params={Promise.resolve({ action: "summary-selection" })}
      searchParams={searchParams}
    />
  );
}
