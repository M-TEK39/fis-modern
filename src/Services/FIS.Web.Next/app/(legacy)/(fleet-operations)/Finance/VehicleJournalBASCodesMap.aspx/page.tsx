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

async function LegacyBasFundPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>): Promise<never> {
  const query = await searchParams;
  const params = new URLSearchParams({ view: "search" });
  const departmentCode = queryValue(query, "DepartmentID", "departmentID", "DepartmentCode");
  if (departmentCode) params.set("departmentCode", departmentCode);
  redirect(`/finance/financial-allocation/allocate-fund-codes?${params.toString()}`);
}

export default function LegacyBasFundPage(props: Readonly<{ searchParams: Promise<Query> }>) {
  return (
    <StreamedRoute>
      <LegacyBasFundPageContent {...props} />
    </StreamedRoute>
  );
}
