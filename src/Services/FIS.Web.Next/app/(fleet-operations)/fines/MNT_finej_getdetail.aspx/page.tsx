import FineDetailPage, {
  type FineDetailPageProps,
} from "@/app/(fleet-operations)/fines/maintenance/detail/page";

export default function LegacyFineHistoricalDetailPage(props: FineDetailPageProps) {
  return <FineDetailPage {...props} routePath="/fines/MNT_finej_getdetail.aspx" />;
}
