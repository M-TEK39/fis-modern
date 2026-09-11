import { type ReportQuery } from "@/app/(fleet-operations)/reports/_utils";
import { ReportsRoutePage } from "@/app/(fleet-operations)/reports/[slug]/_route";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";

async function LegacyTariffReportsPageContent({
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

export default function LegacyTariffReportsPage(
  props: NonNullable<Parameters<typeof LegacyTariffReportsPageContent>[0]>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LegacyTariffReportsPageContent {...props} />
    </Suspense>
  );
}
