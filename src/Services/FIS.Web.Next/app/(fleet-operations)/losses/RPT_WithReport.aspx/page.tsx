import LossReportPage, {
  type LossReportPageProps,
} from "@/app/(fleet-operations)/losses/reports/[mode]/page";

export default function LegacyReportedLossesReport({
  searchParams,
}: Pick<LossReportPageProps, "searchParams">) {
  return (
    <LossReportPage
      params={Promise.resolve({ mode: "with-report" })}
      searchParams={searchParams}
      routePath="/losses/RPT_WithReport.aspx"
    />
  );
}
