import {
  DriverLicenceListPageRoute,
  type DriverLicenceListPageProps,
} from "@/app/(administration)/validation-data/driver-licenses/_route";

export default function LegacyDriverLicenceListPage(
  props: Pick<DriverLicenceListPageProps, "searchParams">,
) {
  return <DriverLicenceListPageRoute {...props} routePath="/Validation/MNT_DriversLicence.aspx" />;
}
