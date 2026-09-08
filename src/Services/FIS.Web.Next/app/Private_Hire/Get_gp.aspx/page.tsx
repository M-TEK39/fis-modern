import PrivateHireReportPage from "@/app/private-hire/reports/page";

export default function LegacyPrivateHireOneVehicleReport({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <PrivateHireReportPage
      searchParams={searchParams}
      kind="one"
      routePath="/Private_Hire/Get_gp.aspx"
    />
  );
}
