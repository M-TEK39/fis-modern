import TaxiLogsPage from "@/app/taxis/logs/page";
import TaxiRequestsPage from "@/app/taxis/requests/page";

type LegacyPrivateHireTaxiRouteProps = Readonly<{
  params: Promise<{ legacyPath: string[] }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}>;

export default async function LegacyPrivateHireTaxiRoute({
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
