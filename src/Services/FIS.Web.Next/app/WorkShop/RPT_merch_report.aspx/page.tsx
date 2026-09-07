import { WorkshopReportPage } from "@/app/workshop/reports/[mode]/page";

export default function LegacyWorkshopMerchantReport({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <WorkshopReportPage mode="merchants" searchParams={searchParams} routePath="/WorkShop/RPT_merch_report.aspx" />;
}
