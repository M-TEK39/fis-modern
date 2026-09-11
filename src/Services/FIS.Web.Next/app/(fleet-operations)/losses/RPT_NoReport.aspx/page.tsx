import LossReportPage, {
  type LossReportPageProps,
} from "@/app/(fleet-operations)/losses/reports/[mode]/_route";

export default function LegacyOutstandingLossesReport({
  searchParams,
}: Pick<LossReportPageProps, "searchParams">) {
  return (
    <LossReportPage
      params={Promise.resolve({ mode: "no-report" })}
      searchParams={searchParams}
      routePath="/losses/RPT_NoReport.aspx"
    />
  );
}
