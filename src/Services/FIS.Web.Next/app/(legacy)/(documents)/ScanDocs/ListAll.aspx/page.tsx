import { redirect } from "next/navigation";

export default function LegacyLicenseCertificateList() {
  redirect("/licenses/scan-certificate?view=all");
}
