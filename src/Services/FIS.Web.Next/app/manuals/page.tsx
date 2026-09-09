import Link from "next/link";
import Image from "next/image";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import { BookOpen } from "lucide-react";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import { ManualLibrary, type ManualGroup } from "@/app/manuals/manual-library";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { getSession } from "@/lib/session";

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
    <Card className="w-full shadow-none" aria-busy="true">
      <CardContent className="loading-card p-8">
        <span className="spinner" aria-hidden="true" />
        <p>Loading manuals...</p>
      </CardContent>
    </Card>
  );
}

async function ManualsContent() {
  await connection();
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
    <section className="w-full max-w-6xl space-y-6" aria-labelledby="manuals-title">
      <Card className="shadow-none">
        <CardHeader className="gap-6 border-b p-6 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex min-w-0 items-start gap-4">
            <span className="flex size-12 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-white p-1 ring-1 ring-border">
              <Image
                src="/logo/gauteng-g-fleet.webp"
                alt=""
                width={48}
                height={48}
                className="size-full object-contain"
                priority
              />
            </span>
            <div>
              <p className="eyebrow">Reference library</p>
              <h1
                id="manuals-title"
                className="text-2xl font-semibold leading-tight tracking-tight sm:text-3xl"
              >
                Gauteng Fleet Information System User Manuals
              </h1>
              <p className="mt-2 text-base text-muted-foreground">
                Open the user manual for the module you need.
              </p>
            </div>
          </div>
          <div className="flex shrink-0 flex-wrap gap-2">
            <Button asChild variant="outline">
              <Link href="/home">Home</Link>
            </Button>
            <form action={logoutAction}>
              <Button type="submit" variant="outline">
                Sign out
              </Button>
            </form>
          </div>
        </CardHeader>
        <CardContent className="p-6">
          <div className="mb-5 flex items-center gap-2 text-sm text-muted-foreground">
            <BookOpen className="size-4" aria-hidden="true" />
            Select a module to see its available documentation.
          </div>
          <ManualLibrary groups={MANUAL_GROUPS} />
        </CardContent>
      </Card>
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
