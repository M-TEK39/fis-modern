import PrivateHireReportPage from "@/app/private-hire/reports/page";

export default function LegacyPrivateHireDepartmentReport({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <PrivateHireReportPage
      searchParams={searchParams}
      kind="department"
      routePath="/Private_Hire/RPT_phv_vehicle_per_dept1.aspx"
    />
  );
}
