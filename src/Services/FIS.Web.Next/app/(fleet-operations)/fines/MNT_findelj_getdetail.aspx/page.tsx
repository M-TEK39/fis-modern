import FineDeleteDetailPage, {
  type FineDeleteDetailPageProps,
} from "@/app/(fleet-operations)/fines/delete/detail/page";

export default function LegacyFineHistoricalDeleteDetailPage(props: FineDeleteDetailPageProps) {
  return <FineDeleteDetailPage {...props} routePath="/fines/MNT_findelj_getdetail.aspx" />;
}
