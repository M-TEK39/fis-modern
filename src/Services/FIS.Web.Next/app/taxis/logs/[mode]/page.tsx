import TaxiLogsPage from "@/app/taxis/logs/page";

export default async function TaxiLogModePage({
  params,
  searchParams,
}: Readonly<{
  params: Promise<{ mode: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}>) {
  return <TaxiLogsPage searchParams={searchParams} mode={(await params).mode} />;
}
