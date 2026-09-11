import { redirect } from "next/navigation";
import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

type LegacyClearanceUniversalProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function toQueryString(query: Record<string, string | string[] | undefined>) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (Array.isArray(value)) {
      for (const item of value) params.append(key, item);
    } else if (value !== undefined) {
      params.set(key, value);
    }
  }
  return params.toString();
}

async function LegacyClearanceUniversalContent({ searchParams }: LegacyClearanceUniversalProps) {
  const query = toQueryString(await searchParams);
  return redirect(`/clearance/reports/universal${query ? `?${query}` : ""}`);
}

export default function LegacyClearanceUniversalPage(props: LegacyClearanceUniversalProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LegacyClearanceUniversalContent {...props} />
    </Suspense>
  );
}
