import LossReportsMenu from "@/app/(fleet-operations)/losses/reports/reports-menu";

export default function LegacyLossesReportsPage() {
  return (
    <LossReportsMenu
      routePath="/losses/Losses.aspx"
      linkBase="/losses/reports"
      backHref="/losses"
    />
  );
}
