import { redirect } from "next/navigation";

export default function LegacyLicenseCertificateUpload() {
  redirect("/licenses/scan-certificate?view=vehicle");
}
