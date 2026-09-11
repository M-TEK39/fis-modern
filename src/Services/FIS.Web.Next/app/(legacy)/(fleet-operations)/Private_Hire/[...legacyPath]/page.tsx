import TaxiLogsPage from "@/app/(fleet-operations)/taxis/logs/page";
import TaxiRequestsPage from "@/app/(fleet-operations)/taxis/requests/page";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type LegacyPrivateHireTaxiRouteProps = Readonly<{
  params: Promise<{ legacyPath: string[] }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}>;

async function LegacyPrivateHireTaxiRouteContent({
  params,
  searchParams,
}: LegacyPrivateHireTaxiRouteProps) {
  const path = (await params).legacyPath.join("/").toLowerCase();
  if (path.includes("log") || path.includes("white_log"))
    return (
      <TaxiLogsPage
        searchParams={searchParams}
        mode={
          path.includes("white_log")
            ? "white-log"
            : path.includes("edit")
              ? "edit"
              : path.includes("reprint")
                ? "reprint"
                : "enter"
        }
      />
    );
  return (
    <TaxiRequestsPage
      searchParams={searchParams}
      mode={
        path.includes("cancel")
          ? "cancel"
          : path.includes("reprint")
            ? "reprint"
            : path.includes("edit")
              ? "edit"
              : "add"
      }
    />
  );
}

export default function LegacyPrivateHireTaxiRoute(props: LegacyPrivateHireTaxiRouteProps) {
  return (
    <StreamedRoute>
      <LegacyPrivateHireTaxiRouteContent {...props} />
    </StreamedRoute>
  );
}
