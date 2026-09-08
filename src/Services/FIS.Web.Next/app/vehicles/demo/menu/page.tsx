import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { hasDemoVehicleRole } from "@/app/vehicles/demo/access";
import {
  DemoAccessRestricted,
  DemoApiUnavailable,
  DemoSessionRecovery,
} from "@/app/vehicles/demo/page-support";
import { getSession } from "@/lib/session";

export default async function DemoVehicleMenuPage({
  routePath = "/vehicles/demo/menu",
}: Readonly<{ routePath?: string }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <DemoSessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <DemoApiUnavailable message="The sign-in service is temporarily unavailable." />
      </main>
    );
  if (!hasDemoVehicleRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <DemoAccessRestricted message="You do not have permission to maintain demo vehicles." />
      </main>
    );
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="demo-menu-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Vehicle Master / Demo Vehicles</p>
            <h1 id="demo-menu-title">Demo Vehicle Maintenance Menu</h1>
            <p>Maintain demo vehicles and review the report.</p>
          </div>
          <Link className="button button-secondary" href="/vehicles">
            Vehicle Master
          </Link>
        </header>
        <div className="vehicle-menu-tiles">
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Demo vehicle maintenance</h2>
            <div className="vehicle-menu-body">
              <Link className="vehicle-menu-link" href="/vehicles/demo/add">
                1) Add a Demo Vehicle
              </Link>
              <Link className="vehicle-menu-link" href="/vehicles/demo/edit">
                2) Edit a Demo Vehicle
              </Link>
              <Link className="vehicle-menu-link" href="/vehicles/demo/delete">
                3) Delete a Demo Vehicle
              </Link>
              <Link className="vehicle-menu-link" href="/vehicles/demo/report">
                4) Report All Demo Vehicles
              </Link>
            </div>
          </section>
        </div>
      </section>
    </main>
  );
}
