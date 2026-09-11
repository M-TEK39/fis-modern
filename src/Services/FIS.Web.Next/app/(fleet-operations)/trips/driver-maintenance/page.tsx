import { Suspense } from "react";
import { redirect } from "next/navigation";

import RouteLoading from "@/components/app-shell/route-loading";

async function TripDriverMaintenanceRedirect(): Promise<never> {
  redirect("/drivers");
}

export default function TripDriverMaintenancePage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TripDriverMaintenanceRedirect />
    </Suspense>
  );
}
