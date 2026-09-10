import ClearanceReportsPage from "@/app/(fleet-operations)/clearance/reports/page";

type LegacyClearanceReportsProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyClearanceReportsPage(_: LegacyClearanceReportsProps) {
  return <ClearanceReportsPage routePath="/Clearance/RPTClearance.aspx" />;
}
