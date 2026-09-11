import {
  LicenseFeeListPageRoute,
  type LicenseFeeListPageProps,
} from "@/app/(administration)/validation-data/license-fees/_route";

export default function LegacyLicenseFeeListPage(
  props: Pick<LicenseFeeListPageProps, "searchParams">,
) {
  return <LicenseFeeListPageRoute {...props} routePath="/Validation/MNT_Licence_Fees.aspx" />;
}
