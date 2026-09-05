import FineDeletePage, { type FineDeletePageProps } from "@/app/fines/delete/page";

export default function LegacyFineDeleteResultPage(props: FineDeletePageProps) {
  return <FineDeletePage {...props} routePath="/fines/MNT_findel_getregfout.aspx" />;
}
