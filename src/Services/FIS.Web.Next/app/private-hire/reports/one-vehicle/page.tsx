import PrivateHireReportPage from "@/app/private-hire/reports/page";

export default function OnePrivateHireVehicleReport({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <PrivateHireReportPage
      searchParams={searchParams}
      kind="one"
      routePath="/private-hire/reports/one-vehicle"
    />
  );
}
