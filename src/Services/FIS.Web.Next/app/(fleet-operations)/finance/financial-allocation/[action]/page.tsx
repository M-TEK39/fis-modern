import { Suspense } from "react";

import { FinancialAllocationRoute } from "@/app/(fleet-operations)/finance/financial-allocation/_route";
import RouteLoading from "@/components/app-shell/route-loading";

type FinancialAllocationActionPageProps = Readonly<{
  params: Promise<{ action: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}>;

export default function FinancialAllocationActionPage({
  params,
  searchParams,
}: FinancialAllocationActionPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <FinancialAllocationActionContent params={params} searchParams={searchParams} />
    </Suspense>
  );
}

async function FinancialAllocationActionContent({
  params,
  searchParams,
}: FinancialAllocationActionPageProps) {
  const { action } = await params;
  return <FinancialAllocationRoute action={action} searchParams={searchParams} />;
}
