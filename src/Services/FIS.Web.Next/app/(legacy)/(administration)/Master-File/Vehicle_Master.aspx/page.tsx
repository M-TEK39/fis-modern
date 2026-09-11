import { Suspense } from "react";

import VehicleMasterPage from "@/app/(fleet-operations)/vehicles/_route";
import RouteLoading from "@/components/app-shell/route-loading";

type LegacyVehicleMasterPageProps = {
  searchParams: Promise<{ page?: string | string[] }>;
};

export default function LegacyVehicleMasterPage({ searchParams }: LegacyVehicleMasterPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <VehicleMasterPage routePath="/Master-File/Vehicle_Master.aspx" searchParams={searchParams} />
    </Suspense>
  );
}
