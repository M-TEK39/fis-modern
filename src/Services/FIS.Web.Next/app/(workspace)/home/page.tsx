import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";
import { Button } from "@/components/ui/button";
import { Card, CardHeader, CardTitle, CardContent, CardFooter } from "@/components/ui/card";
import { FileText, ArrowDownToLine } from "lucide-react";

type DocumentLink = {
  title: string;
  href: string;
  description: string;
};

type HomePageProps = {
  searchParams: Promise<{ page?: string | string[] }>;
  routePath?: "/home" | "/Home.aspx";
};

const DOCUMENT_LINKS: readonly DocumentLink[] = [
  {
    title: "Trip Request Form",
    href: "/Docs/TRIP REQUEST FORM.doc",
    description: "Standard template required for issuing a Trip Authority.",
  },
  {
    title: "Electronic Logsheet and Trip Authority Training Manual",
    href: "/Docs/Electronic Log Sheet and Trip Authority Training Manual.doc",
    description: "Step-by-step user training for the electronic logsheet workflow.",
  },
  {
    title: "Transport Circular No. 4 of 2000",
    href: "/Docs/Policy4 of 2000.doc",
    description: "Official transport policy adopted by the Gauteng Provincial Government.",
  },
  {
    title: "Manual: Updating Trip Authorities",
    href: "/Docs/doc/Updating Trip Authorities Manual2.htm",
    description: "Procedural guide for updating existing trip authority records.",
  },
  {
    title: "Trip Request Form (Chauffeur Driven Service)",
    href: "/Docs/private_hire/trip_request_form.htm",
    description: "Chauffeur-driven service requisition for private hire vehicles.",
  },
  {
    title: "Procedures Manual (Chauffeur Driven Service)",
    href: "/Docs/private_hire/Procedures_manual.htm",
    description: "Detailed procedures governing the chauffeur driven service.",
  },
  {
    title: "State Driver Accident Review Form",
    href: "/Docs/State_Driver_Accident_Review.doc",
    description: "Incident form for state driver accident review panels.",
  },
  {
    title: "Accident Sketch Document",
    href: "/Docs/Accident_Sketch.doc",
    description: "Blank sketch template to accompany accident submissions.",
  },
  {
    title: "Accident Form – Page 1 of 4",
    href: "/Docs/Accident form Z181 page 1 of 4.pdf",
    description: "Official Z181 form: incident details.",
  },
  {
    title: "Accident Form – Page 2 of 4",
    href: "/Docs/Accident form Z181 page 2 of 4.pdf",
    description: "Official Z181 form: vehicle particulars.",
  },
  {
    title: "Accident Form – Page 3 of 4",
    href: "/Docs/Accident form Z181 page 3 of 4.pdf",
    description: "Official Z181 form: witness and statement capture.",
  },
  {
    title: "Accident Form – Page 4 of 4",
    href: "/Docs/Accident form Z181 page 4 of 4.pdf",
    description: "Official Z181 form: declaration and approvals.",
  },
  {
    title: "Application for Safe Keeping Vehicle",
    href: "/Docs/Aplication form for safe keeping of vehicles.pdf",
    description: "Application to store vehicles securely under custodianship.",
  },
  {
    title: "Standard Bank Lost Fuel Card Form",
    href: "/Docs/Lost fuel Card.pdf",
    description: "Notification form for lost Standard Bank fleet cards.",
  },
  {
    title: "Standard Bank Damaged Fuel Card Form",
    href: "/Docs/Damaged fuel Card.pdf",
    description: "Replacement request for damaged Standard Bank fleet cards.",
  },
  {
    title: "Manuals",
    href: "/Manuals/Allmanuals.htm",
    description: "Consolidated library of system manuals and references.",
  },
  {
    title: "RFC Form",
    href: "/Docs/DPTRW_RFC.pdf",
    description: "Request for change (RFC) template used by departmental admins.",
  },
  {
    title: "DITC Form",
    href: "/Docs/DITC Form.pdf",
    description: "Driver Information and Trip Confirmation document.",
  },
  {
    title: "FIS Staff Registration and Review Form",
    href: "/Docs/Staff Registration and Review form.pdf",
    description: "Internal form for onboarding and reviewing staff access.",
  },
  {
    title: "FIS User Registration and Review Form",
    href: "/Docs/Client user Registration and Review form.pdf",
    description: "Client user on-boarding and annual review artefact.",
  },
  {
    title: "Manual for Changing Password",
    href: "/Docs/Implementation of User Admin on GGMT system.doc",
    description: "Guidance on rotating passwords inside the User Admin module.",
  },
];

const PAGE_SIZE = 9;
const TOTAL_PAGES = Math.ceil(DOCUMENT_LINKS.length / PAGE_SIZE);

function HomeFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading your workspace...</p>
    </div>
  );
}

async function HomeContent({ searchParams, routePath }: HomePageProps) {
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
          <Link className="button button-primary" href="/home">
            Try again
          </Link>
          <Link className="button button-secondary" href="/login">
            Sign in
          </Link>
        </div>
      </section>
    );
  }

  const query = await searchParams;
  const pageValue = Array.isArray(query.page) ? query.page[0] : query.page;
  const requestedPage = Number.parseInt(pageValue ?? "1", 10);
  const currentPage = Number.isFinite(requestedPage)
    ? Math.min(Math.max(requestedPage, 1), TOTAL_PAGES)
    : 1;
  const visibleDocuments = DOCUMENT_LINKS.slice(
    (currentPage - 1) * PAGE_SIZE,
    currentPage * PAGE_SIZE,
  );
  const currentPath = routePath ?? "/home";
  const pageHref = (page: number) => (page === 1 ? currentPath : `${currentPath}?page=${page}`);

  return (
    <article className="flex w-full flex-col gap-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold tracking-tight">Gauteng Provincial Government</h1>
        <p className="text-sm text-muted-foreground">
          g-FleeT Management · Fleet Information System
        </p>
      </header>

      <p className="text-sm leading-relaxed text-muted-foreground">
        Welcome to the Fleet Information System Web Site of the Gauteng Provincial Government.
        <br />
        This site controls the issuing of Trip Authorities used by the Provincial Government. These
        Authorities can be issued and printed from this site for free.
      </p>

      <section className="space-y-4" aria-labelledby="downloads-title">
        <h2 id="downloads-title" className="text-base font-semibold">
          Pertinent information downloads
        </h2>
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {visibleDocuments.map((document) => (
            <Card className="flex flex-col shadow-none" key={document.href}>
              <CardHeader className="gap-3 p-5">
                <FileText className="size-5 text-muted-foreground" aria-hidden="true" />
                <CardTitle className="text-sm leading-normal">
                  <h3>{document.title}</h3>
                </CardTitle>
              </CardHeader>
              <CardContent className="flex-1 px-5 pb-4 pt-0">
                <p className="text-sm leading-relaxed text-muted-foreground">
                  {document.description}
                </p>
              </CardContent>
              <CardFooter className="px-5 pb-5 pt-0">
                <Button asChild variant="outline" size="sm">
                  <a
                    href={document.href}
                    download={document.href.startsWith("/Docs/") || undefined}
                  >
                    <ArrowDownToLine aria-hidden="true" />
                    Download
                  </a>
                </Button>
              </CardFooter>
            </Card>
          ))}
        </div>

        <nav className="paginator" aria-label="Document pagination">
          {currentPage === 1 ? (
            <span className="paginator-button paginator-button-disabled" aria-disabled="true">
              Previous
            </span>
          ) : (
            <Link className="paginator-button" href={pageHref(currentPage - 1)}>
              Previous
            </Link>
          )}
          <span aria-live="polite">
            Page {currentPage} of {TOTAL_PAGES}
          </span>
          {currentPage === TOTAL_PAGES ? (
            <span className="paginator-button paginator-button-disabled" aria-disabled="true">
              Next
            </span>
          ) : (
            <Link className="paginator-button" href={pageHref(currentPage + 1)}>
              Next
            </Link>
          )}
        </nav>
      </section>

      <p className="border-t pt-4 text-xs text-muted-foreground">
        Please note that all actions on this site are logged and will be audited from time to time.
      </p>

      <p className="text-xs text-muted-foreground">
        ADOBE ACROBAT might be required to view some of the files on this site. Please click below
        to download the latest version for free from Adobe.
      </p>

      <a
        className="w-fit text-xs underline underline-offset-4"
        href="https://www.adobe.com/products/acrobat/readstep2.html"
      >
        Get Adobe Acrobat Reader
      </a>

      <div className="home-footer-actions">
        <Link className="button button-secondary" href="/change-password">
          Change password
        </Link>
        <form action={logoutAction}>
          <button className="button button-secondary" type="submit">
            Sign out
          </button>
        </form>
      </div>
    </article>
  );
}

export default function HomePage({ searchParams, routePath = "/home" }: HomePageProps) {
  return (
    <main className="page-shell home-page-shell">
      <Suspense fallback={<HomeFallback />}>
        <HomeContent searchParams={searchParams} routePath={routePath} />
      </Suspense>
    </main>
  );
}
