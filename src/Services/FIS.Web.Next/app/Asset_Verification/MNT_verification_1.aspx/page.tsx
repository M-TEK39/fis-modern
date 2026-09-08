import { VehicleVerificationSearchPage } from "@/app/vehicle-verification/search-page";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function LegacyAssetVerificationAdd({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <VehicleVerificationSearchPage
      mode="add"
      searchParams={searchParams}
      routePath="/Asset_Verification/MNT_verification_1.aspx"
    />
  );
}
