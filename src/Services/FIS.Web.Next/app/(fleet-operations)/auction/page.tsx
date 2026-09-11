import Link from "next/link";
import { Gavel } from "lucide-react";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import ModulePageHeader from "@/components/app-shell/module-page-header";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import { MenuSection } from "@/components/ui/menu-section";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";

const REPORTS_ROLE = "Reports";

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

async function AuctionPageContent() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/auction" />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Auction could not be opened.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
        </section>
      </main>
    );
  }

  if (!hasRole(session.roles, REPORTS_ROLE)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to access Auction.</h2>
          <p className="muted-copy">This menu requires the Reports role.</p>
        </section>
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="auction-title">
        <ModulePageHeader
          icon={Gavel}
          eyebrow="Auction"
          title="Auction Maintenance Menu"
          titleId="auction-title"
          description="Maintain Johannesburg auction records and open the associated reports."
          actions={
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          }
        />
        <div className="vehicle-menu-tiles">
          <MenuSection title="Auction Maintenance Information">
            <Link className="vehicle-menu-link" href="/auction/help">
              Auction Maintenance Information / Help
            </Link>
          </MenuSection>
          <MenuSection title="Auction Maintenance for Johannesburg Garage">
            <Link className="vehicle-menu-link" href="/auction/maintenance">
              1) Auction Maintenance
            </Link>
            <Link className="vehicle-menu-link" href="/auction/delete-vehicle">
              2) Delete a Vehicle on Auction
            </Link>
          </MenuSection>
          <MenuSection title="Auction Reports Menu">
            <Link className="vehicle-menu-link" href="/auction/reports">
              Open Auction Reports Menu
            </Link>
          </MenuSection>
        </div>
      </section>
    </main>
  );
}

export default function AuctionPage() {
  return (
    <StreamedRoute>
      <AuctionPageContent />
    </StreamedRoute>
  );
}
