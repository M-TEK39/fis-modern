import { VehicleVerificationSearchPage } from "@/app/vehicle-verification/search-page";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function LegacyAssetVerificationEdit({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  return <VehicleVerificationSearchPage mode="edit" searchParams={searchParams} routePath="/Asset_Verification/MNT_verification_4.aspx" />;
}
