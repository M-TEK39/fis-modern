import { redirect } from "next/navigation";

type Query = Record<string, string | string[] | undefined>;

function queryValue(query: Query, ...names: string[]) {
  for (const name of names) {
    const value = query[name];
    if (value !== undefined) return Array.isArray(value) ? value[0] ?? "" : value;
  }
  return "";
}

export default async function LegacyFinanceSearchParametersPage({ searchParams }: Readonly<{ searchParams: Promise<Query> }>) {
  const query = await searchParams;
  const item = queryValue(query, "Item", "item").toLowerCase();
  const action = item === "invoicedamountspermonth" ? "income-department" : item === "invoicedamountspermonthpersite" ? "income-department-site" : item === "invoicedamountspermonthpersitepervehicle" ? "download-income-department-site-vehicle" : "income-department";
  redirect(`/finance/reports/${action}`);
}
