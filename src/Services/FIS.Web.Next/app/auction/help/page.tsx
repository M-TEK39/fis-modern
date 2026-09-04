import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

const REPORTS_ROLE = "Reports";

type HelpEntry = {
  term: string;
  description: string;
};

const HELP_ENTRIES: readonly HelpEntry[] = [
  {
    term: "Auction Maintenance",
    description: "Capturing and updating data for vehicles that are proposed for auction.",
  },
  {
    term: "Delete a Vehicle on Auction",
    description: "Removes an identified vehicle and its auction-related information when it is no longer required for withdrawal.",
  },
  {
    term: "Auction Number",
    description: "Identifies the date or auction event on which the specified vehicle is proposed to be auctioned.",
  },
  {
    term: "Camp",
    description: "The auction camp where the vehicle will remain during the proposed auction.",
  },
  {
    term: "Lot Number",
    description: "The unique number allocated to the vehicle, usually indicating the sequence in which vehicles will be auctioned.",
  },
  {
    term: "Bar Code",
    description: "The vehicle's secondary identification barcode.",
  },
  {
    term: "Auth. Number",
    description: "The authority number given by the Board of Surveys to dispose of the specified vehicle.",
  },
  {
    term: "Auth Date",
    description: "The date on which the Board of Surveys convened.",
  },
  {
    term: "Km",
    description: "The odometer reading on the vehicle proposed for withdrawal.",
  },
  {
    term: "Sold Reason",
    description: "The reason for withdrawing the vehicle, such as uneconomical repair or another approved reason.",
  },
  {
    term: "Estimated Price",
    description: "The price estimated by the specialist according to the applicable valuation guidance.",
  },
  {
    term: "Reserve Price",
    description: "The lowest price that will be accepted on the day of the auction.",
  },
  {
    term: "Sold Price",
    description: "The price for which the vehicle was sold on the day of the auction.",
  },
  {
    term: "Sold To Name",
    description: "The person or organisation to whom the vehicle was sold.",
  },
  {
    term: "Sold To ID",
    description: "The identification number of the person to whom the vehicle was sold.",
  },
  {
    term: "Sold Date",
    description: "The date on which the vehicle was sold.",
  },
  {
    term: "Remarks",
    description: "Additional information relevant to the vehicle.",
  },
];

function hasRole(roles: readonly string[], role: string) {
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Auction help.</h2>
      <p className="muted-copy">This menu requires the Reports role.</p>
    </section>
  );
}

export default async function AuctionHelpPage() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/auction/help" />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <div className="status-icon status-icon-error" aria-hidden="true">
            !
          </div>
          <p className="eyebrow">API unavailable</p>
          <h2>Auction help could not be opened.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
          <div className="button-row">
            <Link className="button button-primary" href="/auction/help">
              Try again
            </Link>
            <Link className="button button-secondary" href="/login">
              Sign in
            </Link>
          </div>
        </section>
      </main>
    );
  }

  if (!hasRole(session.roles, REPORTS_ROLE)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card" aria-labelledby="auction-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Auction maintenance</p>
            <h1 id="auction-help-title">Auction Maintenance Information / Help</h1>
            <p>Definitions and guidance for the Auction fields.</p>
          </div>
          <Link className="button button-secondary" href="/auction">
            Auction Menu
          </Link>
        </header>

        <section aria-labelledby="auction-help-purpose-title">
          <h2 id="auction-help-purpose-title">Purpose of Program</h2>
          <p>
            The Auctions program provides a uniform process for capturing auction information and analysing the
            outcome of vehicles proposed for disposal.
          </p>
          <p>
            Users of the GGMT administrative functions are expected to have received on-the-job training for the
            fields relevant to their division.
          </p>
        </section>

        <section aria-labelledby="auction-help-fields-title">
          <h2 id="auction-help-fields-title">Term / Field Analysis</h2>
          <p>The required data fields are mostly self-explanatory; the definitions below specify their intended meaning.</p>
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">Auction maintenance terms and field definitions</caption>
              <thead>
                <tr>
                  <th scope="col">Term / Field</th>
                  <th scope="col">Definition</th>
                </tr>
              </thead>
              <tbody>
                {HELP_ENTRIES.map((entry) => (
                  <tr key={entry.term}>
                    <th scope="row">{entry.term}</th>
                    <td>{entry.description}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </article>
    </main>
  );
}
