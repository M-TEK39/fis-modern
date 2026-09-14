import WesbankSiteVehicleDetailPage from "@/app/(fleet-operations)/finance/wesbank/site-vehicle-detail/page";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type Query = Record<string, string | string[] | undefined>;

export default function LegacyWesbankSiteVehicleDetailPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  return (
    <StreamedRoute>
      <WesbankSiteVehicleDetailPage searchParams={searchParams} />
    </StreamedRoute>
  );
}
