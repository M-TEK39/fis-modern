import { VehicleVerificationSearchPage } from "@/app/vehicle-verification/search-page";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function EditAssetVerificationPage({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  return <VehicleVerificationSearchPage mode="edit" searchParams={searchParams} routePath="/vehicle-verification/edit" />;
}
