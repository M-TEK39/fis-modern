import Link from "next/link";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

type ManualLink = {
  label: string;
  href: string;
};

type ManualGroup = {
  title: string;
  links: readonly ManualLink[];
};

const MANUAL_GROUPS: readonly ManualGroup[] = [
  {
    title: "Accidents",
    links: [{ label: "Accidents User Manual", href: "/Accident/Doc/Doc_Accidents.htm" }],
  },
  {
    title: "Auction",
    links: [{ label: "Auction User Manual", href: "/Auction/Doc/Doc_Auctions.htm" }],
  },
  {
    title: "Callcentre",
    links: [{ label: "Callcentre User Manual", href: "/CallCentre/Doc/Doc_CallCentre.htm" }],
  },
  {
    title: "Contracts",
    links: [
      {
        label: "Contracts User Manual",
        href: "/contracts/Docs/User Documentation for Contracts Module.html",
      },
      {
        label: "Training manual - Updated Vehicle Contracts management",
        href: "/Docs/Training_Manual_Vehicle_Contracts_Management.pdf",
      },
    ],
  },
  {
    title: "Fines",
    links: [{ label: "Fines User Manuals", href: "/Fines/Doc/Doc_Fines.htm" }],
  },
  {
    title: "Fuel Card",
    links: [{ label: "Fuel Card User Manuals", href: "/fuelcard/Doc/Doc_Fuelcards.htm" }],
  },
  {
    title: "Licence",
    links: [{ label: "Licence User Manual", href: "/License/Doc/DOC_LICENCE.htm" }],
  },
  {
    title: "Logbook",
    links: [{ label: "Logbook User Manual", href: "/Logbook/Doc/Doc_Logbooks.htm" }],
  },
  {
    title: "Logs",
    links: [{ label: "Logsheets User Manual", href: "/Logs/Doc/Doc_Logsheets.htm" }],
  },
  {
    title: "Losses",
    links: [{ label: "Losses User Manual", href: "/Losses/Doc/DOC_Losses.htm" }],
  },
  {
    title: "Private Hire",
    links: [{ label: "Private Hire User Manual", href: "/Private_Hire/Doc/Doc_PrivateHire.htm" }],
  },
  {
    title: "Reports",
    links: [{ label: "Reports User Manual", href: "/Doc/Doc_Reports.htm" }],
  },
  {
    title: "Tariffs",
    links: [
      {
        label:
          "Training Manual - Annual Tariff Parameters & Overhead & Maintenance Values management",
        href: "/Docs/Training_Manual_Annual_Tariff_Parameters_Overhead_Maintenance_ Values_Management.pdf",
      },
    ],
  },
  {
    title: "Taxis",
    links: [{ label: "Troubleshoot User Manual", href: "/Taxis/Doc/Doc_taxis.htm" }],
  },
  {
    title: "Validation",
    links: [
      { label: "Validation Data User Manual", href: "/Validation/Doc/Doc_ValidationData.htm" },
    ],
  },
  {
    title: "Workshop",
    links: [{ label: "Workshop User Manual", href: "/workshop/help" }],
  },
];

function ManualsFallback() {
  return (
    <section className="manuals-card" aria-busy="true">
      <div className="loading-card">
        <span className="spinner" aria-hidden="true" />
        <p>Loading manuals...</p>
      </div>
    </section>
  );
}

async function ManualsContent() {
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery />;
  }

  if (session.status === "unavailable") {
    return (
      <section className="status-card" role="alert">
        <div className="status-icon status-icon-error" aria-hidden="true">
          !
        </div>
        <p className="eyebrow">API unavailable</p>
        <h1>Your session could not be checked.</h1>
        <p className="muted-copy">
          The application is still running. Retry when the FIS API is available.
        </p>
        <div className="button-row">
          <Link className="button button-primary" href="/manuals">
            Try again
          </Link>
          <Link className="button button-secondary" href="/login">
            Sign in
          </Link>
        </div>
      </section>
    );
  }

  return (
    <section className="manuals-card" aria-labelledby="manuals-title">
      <div className="manuals-header">
        <div className="brand">
          <div className="brand-mark" aria-hidden="true">
            FIS
          </div>
          <div>
            <p className="brand-name">Fleet Information System</p>
            <p className="brand-caption">Gauteng Provincial Government</p>
          </div>
        </div>
        <div className="manuals-title-block">
          <p className="eyebrow">Reference library</p>
          <h1 id="manuals-title">Gauteng Fleet Information System User Manuals</h1>
          <p>Open the user manual for the module you need.</p>
        </div>
        <div className="manuals-actions">
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
          <form action={logoutAction}>
            <button className="button button-secondary" type="submit">
              Sign out
            </button>
          </form>
        </div>
      </div>

      <div className="manuals-tree" aria-label="User manuals">
        {MANUAL_GROUPS.map((group) => (
          <details className="manual-group" key={group.title}>
            <summary>{group.title}</summary>
            <ul>
              {group.links.map((link) => (
                <li key={link.href}>
                  <a href={link.href}>{link.label}</a>
                </li>
              ))}
            </ul>
          </details>
        ))}
      </div>

      <div className="manuals-footer">
        <Link className="button button-secondary" href="/home">
          Back to Home
        </Link>
      </div>
    </section>
  );
}

export default function ManualsPage() {
  return (
    <main className="page-shell manuals-page-shell">
      <Suspense fallback={<ManualsFallback />}>
        <ManualsContent />
      </Suspense>
    </main>
  );
}
