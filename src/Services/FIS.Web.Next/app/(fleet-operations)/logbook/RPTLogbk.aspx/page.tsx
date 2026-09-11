import { type ReportQuery } from "@/app/(fleet-operations)/reports/_utils";
import { ReportsRoutePage } from "@/app/(fleet-operations)/reports/[slug]/_route";

export default function LegacyLogbookReportsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<ReportQuery> }>) {
  return <ReportsRoutePage slug="logbooks" searchParams={searchParams} />;
}
