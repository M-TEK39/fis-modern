import { redirect } from "next/navigation";

export default function LegacyLicenseCertificateSearch() {
  redirect("/licenses/scan-certificate?view=vehicle");
}
