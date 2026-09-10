import FineDeleteDetailPage from "@/app/(fleet-operations)/fines/delete/detail/page";
import type { FineDeleteDetailPageProps } from "@/app/(fleet-operations)/fines/delete/detail/page";

export default function LegacyFineDeleteDetailPage(props: FineDeleteDetailPageProps) {
  return <FineDeleteDetailPage {...props} routePath="/fines/MNT_findel_getdetail.aspx" />;
}
