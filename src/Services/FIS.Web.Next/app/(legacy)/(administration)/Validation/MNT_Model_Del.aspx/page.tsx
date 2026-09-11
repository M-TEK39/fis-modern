import { redirect } from "next/navigation";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";

type ModelDeletePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function ModelDeletePageContent({ searchParams }: ModelDeletePageProps): Promise<never> {
  const query = await searchParams;
  const code = getQueryValue(query.code) ?? getQueryValue(query.modelCode) ?? "";
  redirect(`/Validation/MNT_Model_Del_Check.aspx?code=${encodeURIComponent(code)}`);
}

export default function ModelDeletePage(
  props: NonNullable<Parameters<typeof ModelDeletePageContent>[0]>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <ModelDeletePageContent {...props} />
    </Suspense>
  );
}
