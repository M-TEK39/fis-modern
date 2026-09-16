import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";
import { redirect } from "next/navigation";

import { hasLegacyRole } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { createMakeAction } from "@/app/(administration)/validation-data/makes/actions";
import MakeForm from "@/app/(administration)/validation-data/makes/make-form";
import { getSession } from "@/lib/auth/session";

async function MakeAddPageContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Make_Add.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Service unavailable</p>
          <h2>Make maintenance could not be opened.</h2>
        </section>
      </main>
    );
  if (!hasLegacyRole(session.roles, "Validation"))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to add vehicle makes.</h2>
        </section>
      </main>
    );

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="make-add-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Validation / Vehicle</p>
            <h1 id="make-add-title">Add New Make</h1>
            <p>Add a vehicle make to the legacy make table.</p>
          </div>
          <Link className="button button-secondary" href="/Validation/MNT_make.aspx">
            Make Maintenance
          </Link>
        </header>
        <MakeForm
          action={createMakeAction}
          make={{
            makeCode: 0,
            makeDescription: "",
            dateCreated: null,
            dateUpdated: null,
            createdByUserCode: null,
            modifiedByUserCode: null,
          }}
          mode="create"
        />
      </section>
    </main>
  );
}

export default function MakeAddPage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <MakeAddPageContent />
    </Suspense>
  );
}
