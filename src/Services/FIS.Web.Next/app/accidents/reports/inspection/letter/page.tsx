import { connection } from "next/server";
import { Suspense } from "react";

import {
  InspectionLetterContent,
  InspectionLoadingState,
} from "@/app/accidents/reports/inspection/page";

type QueryValue = string | string[] | undefined;

export default async function InspectionLetterReportPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, QueryValue>>;
}) {
  await connection();
  return (
    <main className="page-shell vehicle-page-shell">
      <Suspense fallback={<InspectionLoadingState />}>
        <InspectionLetterContent searchParams={searchParams} />
      </Suspense>
    </main>
  );
}
