import { VehicleVerificationSearchPage } from "@/app/vehicle-verification/search-page";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function LegacyAccidentAssetVerificationEdit({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <VehicleVerificationSearchPage
      mode="edit"
      searchParams={searchParams}
      routePath="/Accident/MNT_Verification_4.aspx"
    />
  );
}
