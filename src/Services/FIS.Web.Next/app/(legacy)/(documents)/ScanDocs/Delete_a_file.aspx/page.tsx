import { redirect } from "next/navigation";

export default function LegacyLicenseCertificateDelete() {
  redirect("/licenses/scan-certificate?view=vehicle");
}
