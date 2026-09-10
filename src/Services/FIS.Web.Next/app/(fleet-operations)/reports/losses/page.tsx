import LossReportsMenu from "@/app/(fleet-operations)/losses/reports/reports-menu";

export default function LegacyLossReportsPage() {
  return (
    <LossReportsMenu
      routePath="/reports/losses"
      linkBase="/reports/losses"
      backHref="/reports/fis-report"
    />
  );
}
