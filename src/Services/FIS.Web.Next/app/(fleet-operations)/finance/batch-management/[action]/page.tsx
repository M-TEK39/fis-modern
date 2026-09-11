import { Suspense } from "react";

import { BatchManagementRoute } from "@/app/(fleet-operations)/finance/batch-management/_route";
import RouteLoading from "@/components/app-shell/route-loading";

type BatchManagementActionPageProps = Readonly<{
  params: Promise<{ action: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}>;

export default function BatchManagementActionPage({
  params,
  searchParams,
}: BatchManagementActionPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <BatchManagementActionContent params={params} searchParams={searchParams} />
    </Suspense>
  );
}

async function BatchManagementActionContent({
  params,
  searchParams,
}: BatchManagementActionPageProps) {
  const { action } = await params;
  return <BatchManagementRoute action={action} searchParams={searchParams} />;
}
