import { redirect } from "next/navigation";

export default function LegacyLicenseCertificateMissing() {
  redirect("/licenses/scan-certificate?view=missing");
}
