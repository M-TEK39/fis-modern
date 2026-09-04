import FineDetailPage, { type FineDetailPageProps } from "@/app/fines/maintenance/detail/page";

export default function LegacyFineDetailPage(props: FineDetailPageProps) {
  return <FineDetailPage {...props} routePath="/fines/MNT_fine_getdetail.aspx" />;
}
