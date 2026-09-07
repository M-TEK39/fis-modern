import PrivateHireReportPage from "@/app/private-hire/reports/page";

export default function LegacyPrivateHireSiteReport({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <PrivateHireReportPage searchParams={searchParams} kind="site" routePath="/Private_Hire/RPT_phv_vehicle_per_site1.aspx" />;
}
