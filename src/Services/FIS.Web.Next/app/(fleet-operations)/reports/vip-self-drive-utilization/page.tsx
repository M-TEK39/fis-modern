import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { ReportsFrame } from "@/app/(fleet-operations)/reports/_components";
import { getSession } from "@/lib/auth/session";

export default async function VipSelfDriveUtilizationPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <ReportsFrame
        title="VIP Self-Drive Rental Utilization"
        description="The reports workspace is temporarily unavailable."
      >
        <p className="notice notice-error">
          The FIS API could not be reached. Retry when it is available.
        </p>
      </ReportsFrame>
    );

  return (
    <ReportsFrame
      title="VIP Self-Drive Rental Utilization"
      description="This report option is not active in the legacy report menu flow."
    >
      <section className="vehicle-empty-state" role="status">
        <p className="eyebrow">Not in active legacy menu</p>
        <h2>Report option unavailable</h2>
        <p>
          Legacy item 9.4 is commented out in the original Reports menu, so this page is
          intentionally not part of active report navigation parity.
        </p>
        <Link className="button button-primary" href="/reports">
          Back to Reports Menu
        </Link>
      </section>
    </ReportsFrame>
  );
}
