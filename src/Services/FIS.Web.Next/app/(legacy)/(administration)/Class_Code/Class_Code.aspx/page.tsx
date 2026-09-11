import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";
import { type ReportQuery } from "@/app/(fleet-operations)/reports/_components";
import { ReportsRoutePage } from "@/app/(fleet-operations)/reports/[slug]/_route";

export default function LegacyClassCodeReportsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<ReportQuery> }>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <ReportsRoutePage slug="class-code" searchParams={searchParams} />
    </Suspense>
  );
}
