import WesbankReportPage from "@/app/finance/wesbank/[action]/page";

type Query = Record<string, string | string[] | undefined>;

export default function LegacyWesbankUniversalReportPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  return (
    <WesbankReportPage
      params={Promise.resolve({ action: "summary-all" })}
      searchParams={searchParams}
    />
  );
}
