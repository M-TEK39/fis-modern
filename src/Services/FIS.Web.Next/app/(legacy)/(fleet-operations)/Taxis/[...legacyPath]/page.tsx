import TaxiHelpPage from "@/app/(fleet-operations)/taxis/help/page";
import TaxiLogsPage from "@/app/(fleet-operations)/taxis/logs/page";
import TaxiMaintenanceInfoPage from "@/app/(fleet-operations)/taxis/maintenance/info/page";
import TaxiReportsPage, {
  type TaxiReportKind,
} from "@/app/(fleet-operations)/taxis/reports/_route";
import TaxiRequestsPage from "@/app/(fleet-operations)/taxis/requests/page";
import TaxiScanRequisitionPage from "@/app/(fleet-operations)/taxis/scan-requisition/page";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type LegacyTaxiRouteProps = Readonly<{
  params: Promise<{ legacyPath: string[] }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}>;

function reportKind(path: string): TaxiReportKind {
  if (path.includes("fin_reports")) return "financial";
  if (path.includes("inservice")) return "taxis-inservice-per-department";
  if (path.includes("per_department")) return "taxis-per-department";
  if (path.includes("one_num")) return "one-taxi-number";
  if (path.includes("countlogs")) return "logs-per-user";
  if (path.includes("requests")) return "old-requisitions";
  if (path.includes("company")) return "taxis-per-company";
  return "logs-requisitions-status";
}

async function LegacyTaxiRouteContent({ params, searchParams }: LegacyTaxiRouteProps) {
  const path = (await params).legacyPath.join("/").toLowerCase();
  if (path.includes("doc")) return <TaxiHelpPage />;
  if (
    path.includes("scan") ||
    path.includes("rekcert") ||
    path.includes("scandoc") ||
    path.includes("listall") ||
    path.includes("list_one") ||
    path.includes("delete_file") ||
    path.includes("no_rekcert") ||
    path.includes("chk_rek") ||
    path.includes("rekfileexists")
  )
    return <TaxiScanRequisitionPage searchParams={searchParams} />;
  if (path.includes("mnt_menu")) return <TaxiMaintenanceInfoPage />;
  if (
    path.includes("rpt") ||
    path.includes("report") ||
    path.includes("no_log") ||
    path.includes("invoiced")
  )
    return <TaxiReportsPage searchParams={searchParams} kind={reportKind(path)} />;
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

export default function LegacyTaxiRoute(props: LegacyTaxiRouteProps) {
  return (
    <StreamedRoute>
      <LegacyTaxiRouteContent {...props} />
    </StreamedRoute>
  );
}
