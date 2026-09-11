import { redirect } from "next/navigation";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";

type LicenseFeeDeletePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function LicenseFeeDeletePageContent({
  searchParams,
}: LicenseFeeDeletePageProps): Promise<never> {
  const query = await searchParams;
  const code = getQueryValue(query.code) ?? getQueryValue(query.licenceFeeCode) ?? "";
  redirect(`/Validation/MNT_Licence_Fee_Del_Check.aspx?code=${encodeURIComponent(code)}`);
}

export default function LicenseFeeDeletePage(
  props: NonNullable<Parameters<typeof LicenseFeeDeletePageContent>[0]>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LicenseFeeDeletePageContent {...props} />
    </Suspense>
  );
}
