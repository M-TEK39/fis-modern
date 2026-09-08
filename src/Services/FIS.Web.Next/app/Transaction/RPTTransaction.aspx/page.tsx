import { type ReportQuery } from "@/app/reports/_components";
import { ReportsRoutePage } from "@/app/reports/[slug]/page";

export default function LegacyWesbankReportsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<ReportQuery> }>) {
  return <ReportsRoutePage slug="wesbank" searchParams={searchParams} />;
}
