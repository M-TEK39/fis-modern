import RegionalFinanceActionPage from "@/app/finance/regional/[action]/page";

type Query = Record<string, string | string[] | undefined>;

export default function LegacyRegionalProvinceReportPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  return (
    <RegionalFinanceActionPage
      params={Promise.resolve({ action: "summary-per-province" })}
      searchParams={searchParams}
    />
  );
}
