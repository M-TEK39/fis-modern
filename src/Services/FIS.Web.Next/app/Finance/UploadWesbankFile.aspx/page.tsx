import { redirect } from "next/navigation";

export default function LegacyUploadWesbankFilePage() {
  redirect("/finance/standard-bank-import");
}
