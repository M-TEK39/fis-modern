import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import { LicenseShell } from "@/app/(fleet-operations)/licenses/_components";
import {
  accessRestricted,
  getLicenseSession,
  hasLicenseAccess,
  sessionMessage,
} from "@/app/(fleet-operations)/licenses/_page";

const REPORT_GROUPS = [
  {
    title: "One Licence Reports",
    links: [
      ["gg-number", "1) Licence Report for a GG Number"],
      ["gp-number", "2) Licence Report for a Prov Reg Number"],
      ["register-number", "3) Licence Report for a Register Number"],
      ["engine-number", "4) Licence Report for an Engine Number"],
      ["chassis-number", "5) Licence Report for a Chassis Number"],
      ["site", "6) Licence Report for a Site"],
    ],
  },
  { title: "All Licence Reports", links: [["all", "7) Licence Report on ALL Vehicles"]] },
  {
    title: "Other Licence Reports",
    links: [
      ["dept-period", "8) Licences, for a Dept, with All Sites, for a period"],
      ["expire-date", "9) Report on Licence EXPIRE DATE"],
      ["month-fees", "10) Licences fees for a month"],
      ["old-expire", '11) Licences of "In Service" Vehicles, with OLD Expire Dates'],
      ["sap", "12) SAP Information"],
      ["cof", "13) COF Information"],
      ["model-fees", "14) table : Make & Model with Licence Fee"],
      ["gg-model-fees", "15) All Vehicles with Make & Model & Tare & Licence Fee"],
      ["workgroup", "16) Data Workgroup - Report for Each Or Group of Vehicle"],
      ["workgroup-latest", "17) Data Workgroup - Only Latest Report for Each Or Group of Vehicle"],
      ["ggmt-received", "18) Licence Received by GGMT : Receiver Name and Date"],
    ],
  },
] as const;

async function LicenseReportsPageContent() {
  const session = await getLicenseSession();
  const problem = sessionMessage(session, "/licenses/reports");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLicenseAccess(session)) return accessRestricted();

  return (
    <LicenseShell title="Licence Reports Menu" description="Licence reports grouped by category.">
      <div className="vehicle-menu-tiles">
        {REPORT_GROUPS.map((group) => (
          <section className="vehicle-menu-tile" key={group.title}>
            <h2 className="vehicle-menu-header">{group.title}</h2>
            <div className="vehicle-menu-body">
              {group.links.map(([mode, label]) => (
                <Link className="vehicle-menu-link" href={`/licenses/reports/${mode}`} key={mode}>
                  {label}
                </Link>
              ))}
            </div>
          </section>
        ))}
      </div>
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/licenses">
          Licence Menu
        </Link>
        <Link className="button button-secondary" href="/home">
          Home
        </Link>
      </div>
    </LicenseShell>
  );
}

export default function LicenseReportsPage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LicenseReportsPageContent />
    </Suspense>
  );
}
