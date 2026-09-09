import Link from "next/link";
import { BadgeCheck } from "lucide-react";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import ModulePageHeader from "@/app/_components/module-page-header";
import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { MenuSection } from "@/components/ui/menu-section";
import { getSession } from "@/lib/session";

const VALIDATION_LINKS = [
  ["1) Department Maintenance", "/validation-data/departments"],
  ["2) Make Maintenance", "/validation-data/makes"],
  ["3) Model Maintenance", "/validation-data/models"],
  ["4) Site Maintenance", "/validation-data/sites"],
  ["5) Class codes Maintenance", "/validation-data/classes"],
  ["6) License Fees Maintenance", "/validation-data/license-fees"],
  ["7) Drivers license Maintenance", "/validation-data/driver-licenses"],
  ["9) Extras Maintenance", "/validation-data/extras"],
  ["10) Loss Description Maintenance", "/validation-data/loss-types"],
] as const;

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Validation Data.</h2>
      <Link className="button button-secondary" href="/home">
        Home
      </Link>
    </section>
  );
}

export default async function ValidationDataPage() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/validation-data" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Service unavailable</p>
          <h2>Validation Data could not be opened.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
        </section>
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="validation-data-title">
        <ModulePageHeader
          icon={BadgeCheck}
          eyebrow="Validation"
          title="Validation Data"
          titleId="validation-data-title"
          description="Open the same validation-maintenance areas as the legacy menu."
          actions={
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          }
        />
        <div className="vehicle-menu-tiles">
          <MenuSection title="Validation Maintenance Information">
            <Link className="vehicle-menu-link" href="/validation-data/help">
              Validation Data Maintenance Information / Help
            </Link>
          </MenuSection>
          <MenuSection title="Validation Maintenance">
            {VALIDATION_LINKS.map(([label, href]) => (
              <Link className="vehicle-menu-link" href={href} key={href}>
                {label}
              </Link>
            ))}
          </MenuSection>
        </div>
      </section>
    </main>
  );
}
