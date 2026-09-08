import FineDeletePage, { type FineDeletePageProps } from "@/app/fines/delete/page";

export default function LegacyFineHistoricalDeleteResultPage(props: FineDeletePageProps) {
  return <FineDeletePage {...props} routePath="/fines/MNT_findelj_getregfout.aspx" />;
}
