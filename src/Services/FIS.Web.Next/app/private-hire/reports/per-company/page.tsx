import PrivateHireReportPage from "@/app/private-hire/reports/page";

export default function PrivateHireVehiclesPerCompanyReport({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <PrivateHireReportPage searchParams={searchParams} kind="company" routePath="/private-hire/reports/per-company" />;
}
