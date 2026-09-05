import ClearanceUniversalReportPage from "@/app/clearance/reports/universal/page";

type LegacyClearanceUniversalResultsProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyClearanceUniversalResultsPage({ searchParams }: LegacyClearanceUniversalResultsProps) {
  return <ClearanceUniversalReportPage routePath="/Clearance/RPT_Clear_Univ2.aspx" searchParams={searchParams} />;
}
