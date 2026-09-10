import { AssetVerificationDetailsPage } from "@/app/(fleet-operations)/vehicle-verification/details-page";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function EditAssetVerificationDetailsPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <AssetVerificationDetailsPage
      mode="edit"
      searchParams={searchParams}
      routePath="/vehicle-verification/edit/details"
    />
  );
}
