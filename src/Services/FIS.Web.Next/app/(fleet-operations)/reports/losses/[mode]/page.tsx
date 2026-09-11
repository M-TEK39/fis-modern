import LossReportPage from "@/app/(fleet-operations)/losses/reports/[mode]/_route";

type PageProps = {
  params: Promise<{ mode: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyLossReport({ params, searchParams }: Readonly<PageProps>) {
  return <LossReportPage params={params} searchParams={searchParams} routePath="/reports/losses" />;
}
