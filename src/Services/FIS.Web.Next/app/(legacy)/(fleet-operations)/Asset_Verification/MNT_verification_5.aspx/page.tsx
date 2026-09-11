import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function LegacyAssetVerificationEditDetailsContent({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>): Promise<never> {
  const query = await searchParams;
  const gg = queryValue(query.gg) ?? queryValue(query.txtGGNum) ?? "";
  redirect(`/vehicle-verification/edit/details?gg=${encodeURIComponent(gg)}`);
}

export default function LegacyAssetVerificationEditDetails(
  props: Readonly<{ searchParams: SearchParams }>,
) {
  return (
    <StreamedRoute>
      <LegacyAssetVerificationEditDetailsContent {...props} />
    </StreamedRoute>
  );
}
