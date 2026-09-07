import LossReportPage, { type LossReportPageProps } from "@/app/losses/reports/[mode]/page";

export default function LegacyAllLossesReport({ searchParams }: Pick<LossReportPageProps, "searchParams">) {
  return <LossReportPage params={Promise.resolve({ mode: "all" })} searchParams={searchParams} routePath="/losses/RPT_All_Losses_menu.aspx" />;
}
