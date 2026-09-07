import TaxiRequestsPage from "@/app/taxis/requests/page";

export default async function TaxiRequestModePage({ params, searchParams }: Readonly<{ params: Promise<{ mode: string }>; searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <TaxiRequestsPage searchParams={searchParams} mode={(await params).mode} />;
}
