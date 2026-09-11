import { redirect } from "next/navigation";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";

type MakeDeletePageProps = { searchParams: Promise<Record<string, string | string[] | undefined>> };
function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
async function MakeDeletePageContent({ searchParams }: MakeDeletePageProps): Promise<never> {
  const query = await searchParams;
  const code = getQueryValue(query.code) ?? getQueryValue(query.makeCode) ?? "";
  redirect(`/Validation/MNT_Make_Del_Check.aspx?code=${encodeURIComponent(code)}`);
}

export default function MakeDeletePage(
  props: NonNullable<Parameters<typeof MakeDeletePageContent>[0]>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <MakeDeletePageContent {...props} />
    </Suspense>
  );
}
