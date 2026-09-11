import { redirect } from "next/navigation";

import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function MonitorReprintPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const query = await searchParams;
  const reference = query.referenceNumber;
  return redirect(
    `/monitor/reports/one-reference-number${reference ? `?referenceNumber=${encodeURIComponent(Array.isArray(reference) ? (reference[0] ?? "") : reference)}` : ""}`,
  );
}

export default function MonitorReprintPage(
  props: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>,
) {
  return (
    <StreamedRoute>
      <MonitorReprintPageContent {...props} />
    </StreamedRoute>
  );
}
