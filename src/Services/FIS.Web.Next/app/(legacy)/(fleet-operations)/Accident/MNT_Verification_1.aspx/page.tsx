import { VehicleVerificationSearchPage } from "@/app/(fleet-operations)/vehicle-verification/search-page";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function LegacyAccidentAssetVerificationAdd({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <StreamedRoute>
      <VehicleVerificationSearchPage
        mode="add"
        searchParams={searchParams}
        routePath="/Accident/MNT_Verification_1.aspx"
      />
    </StreamedRoute>
  );
}
