import { LicenseReportPage, type LicenseReportPageProps } from "./_route";

export default function LicenseReportRoute({
  params,
  searchParams,
}: Pick<LicenseReportPageProps, "params" | "searchParams">) {
  return <LicenseReportPage params={params} searchParams={searchParams} />;
}
