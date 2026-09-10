import { redirect } from "next/navigation";

type Query = Record<string, string | string[] | undefined>;

function queryValue(query: Query, ...names: string[]) {
  for (const name of names) {
    const value = query[name];
    if (value !== undefined) return (Array.isArray(value) ? (value[0] ?? "") : value).trim();
  }
  return "";
}

export default async function LegacySelectBatchDatePage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  const query = await searchParams;
  const followPage = queryValue(query, "FollowPage", "followPage").toLowerCase();
  redirect(
    `/finance/interface/${followPage.includes("item=exportpastelcsvwithclient") ? "pastel-csv-customer" : "pastel-csv"}`,
  );
}
