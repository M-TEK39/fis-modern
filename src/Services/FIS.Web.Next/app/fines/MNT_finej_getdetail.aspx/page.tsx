import FineDetailPage, { type FineDetailPageProps } from "@/app/fines/maintenance/detail/page";

export default function LegacyFineHistoricalDetailPage(props: FineDetailPageProps) {
  return <FineDetailPage {...props} routePath="/fines/MNT_finej_getdetail.aspx" />;
}
