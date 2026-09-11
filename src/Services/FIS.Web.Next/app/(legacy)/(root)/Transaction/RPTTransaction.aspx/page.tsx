import { type ReportQuery } from "@/app/(fleet-operations)/reports/_components";
import { ReportsRoutePage } from "@/app/(fleet-operations)/reports/[slug]/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

export default function LegacyWesbankReportsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<ReportQuery> }>) {
  return (
    <StreamedRoute>
      <ReportsRoutePage slug="wesbank" searchParams={searchParams} />
    </StreamedRoute>
  );
}
