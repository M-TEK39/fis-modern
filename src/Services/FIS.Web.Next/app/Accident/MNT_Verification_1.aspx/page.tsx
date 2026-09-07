import { VehicleVerificationSearchPage } from "@/app/vehicle-verification/search-page";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function LegacyAccidentAssetVerificationAdd({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  return <VehicleVerificationSearchPage mode="add" searchParams={searchParams} routePath="/Accident/MNT_Verification_1.aspx" />;
}
