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

async function LegacyBasViewPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>): Promise<never> {
  const query = await searchParams;
  const params = new URLSearchParams({ view: "search" });
  const departmentCode = queryValue(query, "DepartmentID", "departmentID", "DepartmentCode");
  if (departmentCode) params.set("departmentCode", departmentCode);
  redirect(`/finance/financial-allocation/view-bas?${params.toString()}`);
}

export default function LegacyBasViewPage(props: Readonly<{ searchParams: Promise<Query> }>) {
  return (
    <StreamedRoute>
      <LegacyBasViewPageContent {...props} />
    </StreamedRoute>
  );
}
