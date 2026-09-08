import { VehicleVerificationSearchPage } from "@/app/vehicle-verification/search-page";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function AddAssetVerificationPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <VehicleVerificationSearchPage
      mode="add"
      searchParams={searchParams}
      routePath="/vehicle-verification/add"
    />
  );
}
