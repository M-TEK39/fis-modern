import { Suspense } from "react";

import GgBlockNumbersPage from "@/app/(fleet-operations)/vehicles/gg-block-numbers/page";
import RouteLoading from "@/components/app-shell/route-loading";

export default function LegacyGgBlockNumbersPage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <GgBlockNumbersPage
        searchParams={Promise.resolve({})}
        routePath="/Master-File/Add_GGBlockNumbers.aspx"
      />
    </Suspense>
  );
}
