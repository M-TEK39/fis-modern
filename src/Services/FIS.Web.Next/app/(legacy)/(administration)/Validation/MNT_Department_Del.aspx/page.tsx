import { redirect } from "next/navigation";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";

type DepartmentDeletePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function DepartmentDeletePageContent({
  searchParams,
}: DepartmentDeletePageProps): Promise<never> {
  const query = await searchParams;
  const code = getQueryValue(query.code) ?? getQueryValue(query.cmbdep) ?? "";
  redirect(`/Validation/MNT_Department_Del_Check.aspx?code=${encodeURIComponent(code)}`);
}

export default function DepartmentDeletePage(
  props: NonNullable<Parameters<typeof DepartmentDeletePageContent>[0]>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <DepartmentDeletePageContent {...props} />
    </Suspense>
  );
}
