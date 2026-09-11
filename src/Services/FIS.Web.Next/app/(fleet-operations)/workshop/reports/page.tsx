import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";
import WorkshopReportsMenu from "@/app/(fleet-operations)/workshop/reports/reports-menu";

export default function WorkshopReportsPage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <WorkshopReportsMenu />
    </Suspense>
  );
}
