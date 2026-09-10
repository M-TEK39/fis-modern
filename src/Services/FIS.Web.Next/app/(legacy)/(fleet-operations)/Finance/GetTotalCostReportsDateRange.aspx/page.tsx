import RegionalFinanceActionPage from "@/app/(fleet-operations)/finance/regional/[action]/page";

type Query = Record<string, string | string[] | undefined>;

export default function LegacyRegionalSummaryReportPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  return (
    <RegionalFinanceActionPage
      params={Promise.resolve({ action: "summary-all" })}
      searchParams={searchParams}
    />
  );
}
