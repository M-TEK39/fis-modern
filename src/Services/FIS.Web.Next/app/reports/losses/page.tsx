import LossReportsMenu from "@/app/losses/reports/reports-menu";

export default function LegacyLossReportsPage() {
  return (
    <LossReportsMenu
      routePath="/reports/losses"
      linkBase="/reports/losses"
      backHref="/reports/fis-report"
    />
  );
}
