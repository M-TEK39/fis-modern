import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import TaxiReportsPage, {
  type TaxiReportKind,
} from "@/app/(fleet-operations)/taxis/reports/_route";

const REPORT_MODES = new Set<TaxiReportKind>([
  "one-taxi-number",
  "logs-per-user",
  "old-requisitions",
  "taxis-per-company",
  "taxis-per-department",
  "taxis-inservice-per-department",
  "logs-requisitions-status",
  "financial",
]);

async function TaxiReportModePageContent({
  params,
  searchParams,
}: Readonly<{
  params: Promise<{ mode: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}>) {
  const mode = (await params).mode as TaxiReportKind;
  return (
    <TaxiReportsPage
      searchParams={searchParams}
      kind={REPORT_MODES.has(mode) ? mode : "one-taxi-number"}
    />
  );
}

export default function TaxiReportModePage(props: Parameters<typeof TaxiReportModePageContent>[0]) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TaxiReportModePageContent {...props} />
    </Suspense>
  );
}
