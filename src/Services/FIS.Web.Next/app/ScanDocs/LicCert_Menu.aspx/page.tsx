import { redirect } from "next/navigation";

export default function LegacyLicenseCertificateMenu() {
  redirect("/licenses/scan-certificate");
}
