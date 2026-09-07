import PrivateHireReportPage from "@/app/private-hire/reports/page";

export default function LegacyPrivateHireDepartmentInServiceReport({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <PrivateHireReportPage searchParams={searchParams} kind="department-in-service" routePath="/Private_Hire/RPT_phv_vehicle_per_dept_inserv1.aspx" />;
}
