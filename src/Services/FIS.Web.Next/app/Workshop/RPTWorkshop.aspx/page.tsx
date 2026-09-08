import WorkshopReportsMenu from "@/app/workshop/reports/reports-menu";

export default function LegacyWorkshopReportsMenu() {
  return (
    <WorkshopReportsMenu
      routePath="/Workshop/RPTWorkshop.aspx"
      linkBase="/reports/workshop"
      backHref="/reports/fis-report"
    />
  );
}
