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

async function LegacyFinanceReportsPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>): Promise<never> {
  const query = await searchParams;
  const mode = queryValue(query, "Mode", "mode").toLowerCase();
  redirect(`/finance/reports/${mode === "site" || mode === "province" ? mode : "department"}`);
}

export default function LegacyFinanceReportsPage(
  props: Readonly<{ searchParams: Promise<Query> }>,
) {
  return (
    <StreamedRoute>
      <LegacyFinanceReportsPageContent {...props} />
    </StreamedRoute>
  );
}
