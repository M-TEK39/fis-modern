import { redirect } from "next/navigation";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function queryValue(value: string | string[] | undefined) { return Array.isArray(value) ? value[0] : value; }

export default async function LegacyAssetVerificationEditDetails({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  const query = await searchParams;
  const gg = queryValue(query.gg) ?? queryValue(query.txtGGNum) ?? "";
  redirect(`/vehicle-verification/edit/details?gg=${encodeURIComponent(gg)}`);
}
