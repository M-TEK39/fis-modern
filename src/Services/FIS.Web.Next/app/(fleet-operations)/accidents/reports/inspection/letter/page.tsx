import { Suspense } from "react";

import {
  InspectionLetterContent,
  InspectionLoadingState,
} from "@/app/(fleet-operations)/accidents/reports/inspection/_route";

type QueryValue = string | string[] | undefined;

export default function InspectionLetterReportPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, QueryValue>>;
}) {
  return (
    <main className="page-shell vehicle-page-shell">
      <Suspense fallback={<InspectionLoadingState />}>
        <InspectionLetterContent searchParams={searchParams} />
      </Suspense>
    </main>
  );
}
