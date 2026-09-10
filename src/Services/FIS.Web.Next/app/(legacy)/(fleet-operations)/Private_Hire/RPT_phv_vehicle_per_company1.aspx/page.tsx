import PrivateHireReportPage from "@/app/(fleet-operations)/private-hire/reports/page";

export default function LegacyPrivateHireCompanyReport({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <PrivateHireReportPage
      searchParams={searchParams}
      kind="company"
      routePath="/Private_Hire/RPT_phv_vehicle_per_company1.aspx"
    />
  );
}
