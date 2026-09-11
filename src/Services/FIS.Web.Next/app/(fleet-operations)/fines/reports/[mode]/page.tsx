import { FineReportRoute } from "./_route";

export default function FineReportPage({
  params,
  searchParams,
}: {
  params: Promise<{ mode: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  return <FineReportRoute params={params} searchParams={searchParams} />;
}
