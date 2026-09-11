import { redirect } from "next/navigation";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";

type LegacyEditFormPageProps = {
  searchParams: Promise<{ GGnum?: string | string[] }>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function LegacyEditFormPageContent({
  searchParams,
}: LegacyEditFormPageProps): Promise<never> {
  const query = await searchParams;
  const ggNumber = getQueryValue(query.GGnum)?.trim();
  const target = ggNumber
    ? `/vehicles/edit?searchTerm=${encodeURIComponent(ggNumber)}`
    : "/vehicles/edit";
  redirect(target);
}

export default function LegacyEditFormPage(
  props: NonNullable<Parameters<typeof LegacyEditFormPageContent>[0]>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LegacyEditFormPageContent {...props} />
    </Suspense>
  );
}
