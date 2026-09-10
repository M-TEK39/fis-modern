import WesbankReportPage from "@/app/(fleet-operations)/finance/wesbank/[action]/page";

type Query = Record<string, string | string[] | undefined>;

export default function LegacyWesbankDetailedProvinceReportPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  return (
    <WesbankReportPage
      params={Promise.resolve({ action: "detailed-selection" })}
      searchParams={searchParams}
    />
  );
}
