import LossTypeListPage from "@/app/validation-data/loss-types/page";

export default function LegacyLossTypeListPage(props: Parameters<typeof LossTypeListPage>[0]) {
  return <LossTypeListPage {...props} routePath="/Validation/MNT_Loss_Type.aspx" />;
}
