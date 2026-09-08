import TaxiReportsPage, { type TaxiReportKind } from "@/app/taxis/reports/page";

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

export default async function TaxiReportModePage({
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
