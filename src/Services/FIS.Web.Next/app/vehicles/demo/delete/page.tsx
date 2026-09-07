import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasDemoVehicleRole } from "@/app/vehicles/demo/access";
import DemoVehicleSearch from "@/app/vehicles/demo/demo-vehicle-search";
import { DemoAccessRestricted, DemoApiUnavailable, DemoSessionRecovery } from "@/app/vehicles/demo/page-support";
import { getSession } from "@/lib/session";

export default async function DemoDeletePage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><DemoSessionRecovery returnPath="/vehicles/demo/delete" /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><DemoApiUnavailable message="The sign-in service is temporarily unavailable." /></main>;
  if (!hasDemoVehicleRole(session.roles)) return <main className="page-shell vehicle-page-shell"><DemoAccessRestricted message="You do not have permission to delete demo vehicles." /></main>;

  return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="demo-delete-title"><header className="vehicle-page-header"><div><p className="eyebrow">Vehicle Master / Demo Vehicles</p><h1 id="demo-delete-title">Delete Demo Vehicle</h1><p>Search by GG or GP number, then confirm deletion of the selected record.</p></div><div className="button-row"><Link className="button button-secondary" href="/vehicles">Vehicle Master</Link></div></header><DemoVehicleSearch mode="delete" /></section></main>;
}
