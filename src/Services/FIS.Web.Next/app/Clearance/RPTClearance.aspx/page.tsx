import ClearanceReportsPage from "@/app/clearance/reports/page";

type LegacyClearanceReportsProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyClearanceReportsPage(_: LegacyClearanceReportsProps) {
  return <ClearanceReportsPage routePath="/Clearance/RPTClearance.aspx" />;
}
