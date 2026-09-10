import { BatchManagementRoute } from "@/app/(fleet-operations)/finance/batch-management/page";

export default async function BatchManagementActionPage({
  params,
  searchParams,
}: Readonly<{
  params: Promise<{ action: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}>) {
  const { action } = await params;
  return <BatchManagementRoute action={action} searchParams={searchParams} />;
}
