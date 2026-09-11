import PrivateHireReportPage from "@/app/(fleet-operations)/private-hire/reports/_route";

export default function PrivateHireVehiclesPerSiteReport({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <PrivateHireReportPage
      searchParams={searchParams}
      kind="site"
      routePath="/private-hire/reports/per-site"
    />
  );
}
