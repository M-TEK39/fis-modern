import { FinancialAllocationRoute } from "@/app/finance/financial-allocation/page";

export default async function FinancialAllocationActionPage({
  params,
  searchParams,
}: Readonly<{
  params: Promise<{ action: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}>) {
  const { action } = await params;
  return <FinancialAllocationRoute action={action} searchParams={searchParams} />;
}
