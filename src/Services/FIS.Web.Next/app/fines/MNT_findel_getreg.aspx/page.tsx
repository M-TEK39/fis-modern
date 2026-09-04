import FineDeletePage from "@/app/fines/delete/page";
import type { FineDeletePageProps } from "@/app/fines/delete/page";

export default function LegacyFineDeletePage(props: FineDeletePageProps) {
  return <FineDeletePage {...props} routePath="/fines/MNT_findel_getreg.aspx" />;
}
