import { redirect } from "next/navigation";

type Query = Record<string, string | string[] | undefined>;

function queryValue(query: Query, ...names: string[]) {
  for (const name of names) {
    const value = query[name];
    if (value !== undefined) return Array.isArray(value) ? value[0] ?? "" : value;
  }
  return "";
}

export default async function LegacyFinanceReportsPage({ searchParams }: Readonly<{ searchParams: Promise<Query> }>) {
  const query = await searchParams;
  const mode = queryValue(query, "Mode", "mode").toLowerCase();
  redirect(`/finance/reports/${mode === "site" || mode === "province" ? mode : "department"}`);
}
