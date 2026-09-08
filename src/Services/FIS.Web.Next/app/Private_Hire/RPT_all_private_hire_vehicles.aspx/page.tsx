import PrivateHireReportPage from "@/app/private-hire/reports/page";

export default function LegacyPrivateHireAllVehiclesReport({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <PrivateHireReportPage
      searchParams={searchParams}
      kind="all"
      routePath="/Private_Hire/RPT_all_private_hire_vehicles.aspx"
    />
  );
}
