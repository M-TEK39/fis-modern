import { type ReportQuery } from "@/app/reports/_components";
import { ReportsRoutePage } from "@/app/reports/[slug]/page";

export default function LegacyDepartmentReportsPage({ searchParams }: Readonly<{ searchParams: Promise<ReportQuery> }>) {
  return <ReportsRoutePage slug="departments-sites" searchParams={searchParams} />;
}
