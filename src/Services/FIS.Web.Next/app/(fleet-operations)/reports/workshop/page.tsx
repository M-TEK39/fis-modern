import WorkshopReportsMenu from "@/app/(fleet-operations)/workshop/reports/reports-menu";

export default function ModernWorkshopReportsPage() {
  return (
    <WorkshopReportsMenu
      routePath="/reports/workshop"
      linkBase="/reports/workshop"
      backHref="/reports/fis-report"
    />
  );
}
