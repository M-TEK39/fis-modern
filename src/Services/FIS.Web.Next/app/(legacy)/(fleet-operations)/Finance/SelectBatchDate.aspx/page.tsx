import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type Query = Record<string, string | string[] | undefined>;

function queryValue(query: Query, ...names: string[]) {
  for (const name of names) {
    const value = query[name];
    if (value !== undefined) return (Array.isArray(value) ? (value[0] ?? "") : value).trim();
  }
  return "";
}

async function LegacySelectBatchDatePageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>): Promise<never> {
  const query = await searchParams;
  const followPage = queryValue(query, "FollowPage", "followPage").toLowerCase();
  redirect(
    `/finance/interface/${followPage.includes("item=exportpastelcsvwithclient") ? "pastel-csv-customer" : "pastel-csv"}`,
  );
}

export default function LegacySelectBatchDatePage(
  props: Readonly<{ searchParams: Promise<Query> }>,
) {
  return (
    <StreamedRoute>
      <LegacySelectBatchDatePageContent {...props} />
    </StreamedRoute>
  );
}
