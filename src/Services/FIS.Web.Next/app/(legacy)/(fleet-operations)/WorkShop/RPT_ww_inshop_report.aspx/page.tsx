import { WorkshopReportPage } from "@/app/(fleet-operations)/workshop/reports/[mode]/_route";

export default function LegacyWorkshopInShopReport({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <WorkshopReportPage
      mode="in-workshop"
      searchParams={searchParams}
      routePath="/WorkShop/RPT_ww_inshop_report.aspx"
    />
  );
}
