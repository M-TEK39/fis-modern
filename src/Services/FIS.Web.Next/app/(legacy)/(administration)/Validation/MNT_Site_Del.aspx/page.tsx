import { redirect } from "next/navigation";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";

type SiteDeletePageProps = { searchParams: Promise<Record<string, string | string[] | undefined>> };
function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function SiteDeletePageContent({ searchParams }: SiteDeletePageProps): Promise<never> {
  const query = await searchParams;
  const code = queryValue(query.code) ?? queryValue(query.siteCode) ?? "";
  redirect(`/Validation/MNT_Site_Del_Check.aspx?code=${encodeURIComponent(code)}`);
}

export default function SiteDeletePage(
  props: NonNullable<Parameters<typeof SiteDeletePageContent>[0]>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <SiteDeletePageContent {...props} />
    </Suspense>
  );
}
