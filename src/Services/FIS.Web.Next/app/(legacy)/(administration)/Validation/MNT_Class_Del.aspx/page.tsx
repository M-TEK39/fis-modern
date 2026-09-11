import { redirect } from "next/navigation";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";

type ClassDeletePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function ClassDeletePageContent({ searchParams }: ClassDeletePageProps): Promise<never> {
  const query = await searchParams;
  const code = getQueryValue(query.code) ?? getQueryValue(query.classCode) ?? "";
  redirect(`/Validation/MNT_Class_Del_Check.aspx?code=${encodeURIComponent(code)}`);
}

export default function ClassDeletePage(
  props: NonNullable<Parameters<typeof ClassDeletePageContent>[0]>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <ClassDeletePageContent {...props} />
    </Suspense>
  );
}
