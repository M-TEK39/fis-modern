import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type Query = Record<string, string | string[] | undefined>;

function queryValue(query: Query, ...names: string[]) {
  for (const name of names) {
    const value = query[name];
    if (value !== undefined) return Array.isArray(value) ? (value[0] ?? "") : value;
  }
  return "";
}

async function LegacyFinanceYearPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>): Promise<never> {
  const query = await searchParams;
  const item = queryValue(query, "Item", "item").toLowerCase();
  const actionParameter = queryValue(query, "action").toLowerCase();
  if (actionParameter === "closekilogaps") redirect("/finance/missing-kilometres/close-gaps");
  if (item === "kilogaps")
    redirect(
      queryValue(query, "OutputFormat").toLowerCase() === "xls"
        ? "/finance/missing-kilometres/kilo-gaps-xls"
        : "/finance/missing-kilometres/kilo-gaps-pdf",
    );
  const action =
    item === "vehiclebillinghistory"
      ? "vehicle-billing-history"
      : item === "summaryincomesplit"
        ? "income-split-summary"
        : item === "detailedincomesplit"
          ? "income-split-detailed"
          : "department";
  redirect(`/finance/reports/${action}`);
}

export default function LegacyFinanceYearPage(props: Readonly<{ searchParams: Promise<Query> }>) {
  return (
    <StreamedRoute>
      <LegacyFinanceYearPageContent {...props} />
    </StreamedRoute>
  );
}
