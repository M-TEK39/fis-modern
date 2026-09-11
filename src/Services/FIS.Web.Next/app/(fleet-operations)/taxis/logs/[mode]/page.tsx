import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import TaxiLogsPage from "@/app/(fleet-operations)/taxis/logs/page";

async function TaxiLogModePageContent({
  params,
  searchParams,
}: Readonly<{
  params: Promise<{ mode: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}>) {
  return <TaxiLogsPage searchParams={searchParams} mode={(await params).mode} />;
}

export default function TaxiLogModePage(props: Parameters<typeof TaxiLogModePageContent>[0]) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TaxiLogModePageContent {...props} />
    </Suspense>
  );
}
