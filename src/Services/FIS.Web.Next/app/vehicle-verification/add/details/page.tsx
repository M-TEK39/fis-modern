import { AssetVerificationDetailsPage } from "@/app/vehicle-verification/details-page";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function AddAssetVerificationDetailsPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <AssetVerificationDetailsPage
      mode="add"
      searchParams={searchParams}
      routePath="/vehicle-verification/add/details"
    />
  );
}
