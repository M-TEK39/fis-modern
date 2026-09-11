import {
  LossTypeListPageRoute,
  type LossTypeListPageProps,
} from "@/app/(administration)/validation-data/loss-types/_route";

export default function LegacyLossTypeListPage(props: Pick<LossTypeListPageProps, "searchParams">) {
  return <LossTypeListPageRoute {...props} routePath="/Validation/MNT_Loss_Type.aspx" />;
}
