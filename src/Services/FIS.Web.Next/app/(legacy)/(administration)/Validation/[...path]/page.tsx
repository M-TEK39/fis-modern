import { type ReportQuery } from "@/app/(fleet-operations)/reports/_components";
import { ReportsRoutePage } from "@/app/(fleet-operations)/reports/[slug]/page";

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
