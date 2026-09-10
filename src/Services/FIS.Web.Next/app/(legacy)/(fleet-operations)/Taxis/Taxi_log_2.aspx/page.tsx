import TaxiLogsPage from "@/app/(fleet-operations)/taxis/logs/page";

export default function LegacyTaxiLogEntryStepTwo({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <TaxiLogsPage searchParams={searchParams} mode="enter" />;
}
