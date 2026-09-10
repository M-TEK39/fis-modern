import FineDeletePage from "@/app/(fleet-operations)/fines/delete/page";
import type { FineDeletePageProps } from "@/app/(fleet-operations)/fines/delete/page";

export default function LegacyFineDeletePage(props: FineDeletePageProps) {
  return <FineDeletePage {...props} routePath="/fines/MNT_findel_getreg.aspx" />;
}
