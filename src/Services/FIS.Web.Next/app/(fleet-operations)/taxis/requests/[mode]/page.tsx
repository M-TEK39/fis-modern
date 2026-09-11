import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import TaxiRequestsPage from "@/app/(fleet-operations)/taxis/requests/page";

async function TaxiRequestModePageContent({
  params,
  searchParams,
}: Readonly<{
  params: Promise<{ mode: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}>) {
  return <TaxiRequestsPage searchParams={searchParams} mode={(await params).mode} />;
}

export default function TaxiRequestModePage(
  props: Parameters<typeof TaxiRequestModePageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TaxiRequestModePageContent {...props} />
    </Suspense>
  );
}
