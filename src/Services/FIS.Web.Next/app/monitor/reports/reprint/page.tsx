import { redirect } from "next/navigation";

export default async function MonitorReprintPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const query = await searchParams;
  const reference = query.referenceNumber;
  redirect(
    `/monitor/reports/one-reference-number${reference ? `?referenceNumber=${encodeURIComponent(Array.isArray(reference) ? (reference[0] ?? "") : reference)}` : ""}`,
  );
}
