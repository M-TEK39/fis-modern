import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";
import { type ReportQuery } from "@/app/(fleet-operations)/reports/_utils";
import { ReportsRoutePage } from "@/app/(fleet-operations)/reports/[slug]/_route";

export default function LegacyDepartmentReportsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<ReportQuery> }>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <ReportsRoutePage slug="departments-sites" searchParams={searchParams} />
    </Suspense>
  );
}
