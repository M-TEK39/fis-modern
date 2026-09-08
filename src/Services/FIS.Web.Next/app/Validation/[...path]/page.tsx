import { type ReportQuery } from "@/app/reports/_components";
import { ReportsRoutePage } from "@/app/reports/[slug]/page";

export default async function LegacyTariffReportsPage({
  params,
  searchParams,
}: Readonly<{ params: Promise<{ path: string[] }>; searchParams: Promise<ReportQuery> }>) {
  const { path } = await params;
  return (
    <ReportsRoutePage
      slug={path.join("/").toLowerCase().includes("tariff") ? "tariffs" : "tariffs"}
      searchParams={searchParams}
    />
  );
}
