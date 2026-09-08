import { redirect } from "next/navigation";

export default function LegacyLicenseCertificateListAll() {
  redirect("/licenses/scan-certificate?view=all");
}
