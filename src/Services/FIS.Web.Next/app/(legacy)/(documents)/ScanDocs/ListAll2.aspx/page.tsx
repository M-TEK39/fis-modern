import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";
import { redirect } from "next/navigation";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function requestedPage(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : 1;
}

async function LegacyLicenseCertificateListAllContent({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>): Promise<never> {
  redirect(
    `/licenses/scan-certificate?${new URLSearchParams({ view: "all", page: String(requestedPage(queryValue((await searchParams).page))) }).toString()}`,
  );
}

export default function LegacyLicenseCertificateListAll({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LegacyLicenseCertificateListAllContent searchParams={searchParams} />
    </Suspense>
  );
}
