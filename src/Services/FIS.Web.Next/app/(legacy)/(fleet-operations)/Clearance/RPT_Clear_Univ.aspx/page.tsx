import ClearanceUniversalReportPage from "@/app/(fleet-operations)/clearance/reports/universal/page";

type LegacyClearanceUniversalProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyClearanceUniversalPage({
  searchParams,
}: LegacyClearanceUniversalProps) {
  return (
    <ClearanceUniversalReportPage
      routePath="/Clearance/RPT_Clear_Univ.aspx"
      searchParams={searchParams}
    />
  );
}
