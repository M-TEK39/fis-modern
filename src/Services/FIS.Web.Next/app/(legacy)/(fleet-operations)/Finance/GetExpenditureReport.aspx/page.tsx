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

async function LegacyOutstandingPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>): Promise<never> {
  const query = await searchParams;
  const departmentCode = queryValue(query, "lstDepartmentId", "depCode", "DepartmentCode");
  redirect(
    departmentCode
      ? `/finance/outstanding/department-site-vehicle?run=1&departmentCode=${encodeURIComponent(departmentCode)}`
      : "/finance/outstanding/department-site-vehicle",
  );
}

export default function LegacyOutstandingPage(props: Readonly<{ searchParams: Promise<Query> }>) {
  return (
    <StreamedRoute>
      <LegacyOutstandingPageContent {...props} />
    </StreamedRoute>
  );
}
