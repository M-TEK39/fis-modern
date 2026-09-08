import { redirect } from "next/navigation";

export default function LegacyAuditTrailPage() {
  redirect("/finance/audit-trail");
}
