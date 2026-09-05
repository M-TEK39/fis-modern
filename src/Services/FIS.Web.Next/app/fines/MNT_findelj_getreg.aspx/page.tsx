import FineDeletePage, { type FineDeletePageProps } from "@/app/fines/delete/page";

export default function LegacyFineHistoricalDeletePage(props: FineDeletePageProps) {
  return <FineDeletePage {...props} routePath="/fines/MNT_findelj_getreg.aspx" />;
}
