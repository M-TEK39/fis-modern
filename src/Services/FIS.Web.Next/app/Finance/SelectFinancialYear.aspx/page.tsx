import { redirect } from "next/navigation";

type Query = Record<string, string | string[] | undefined>;

function queryValue(query: Query, ...names: string[]) {
  for (const name of names) {
    const value = query[name];
    if (value !== undefined) return Array.isArray(value) ? value[0] ?? "" : value;
  }
  return "";
}

export default async function LegacyFinanceYearPage({ searchParams }: Readonly<{ searchParams: Promise<Query> }>) {
  const query = await searchParams;
  const item = queryValue(query, "Item", "item").toLowerCase();
  const action = item === "vehiclebillinghistory" ? "vehicle-billing-history" : item === "summaryincomesplit" ? "income-split-summary" : item === "detailedincomesplit" ? "income-split-detailed" : "department";
  redirect(`/finance/reports/${action}`);
}
