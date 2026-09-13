import JournalInvoicePage from "@/app/(fleet-operations)/finance/journal-invoice/page";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type Query = Record<string, string | string[] | undefined>;

export default function LegacyJournalInvoicePage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  return (
    <StreamedRoute>
      <JournalInvoicePage searchParams={searchParams} />
    </StreamedRoute>
  );
}
