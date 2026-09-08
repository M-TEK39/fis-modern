import WesbankReportPage from "@/app/finance/wesbank/[action]/page";

type Query = Record<string, string | string[] | undefined>;

export default function LegacyWesbankDetailedReportPage({ searchParams }: Readonly<{ searchParams: Promise<Query> }>) {
  return <WesbankReportPage params={Promise.resolve({ action: "detailed-all" })} searchParams={searchParams} />;
}
