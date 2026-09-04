import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

function UnavailableState() {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-status-card" role="alert">
        <div className="status-icon status-icon-error" aria-hidden="true">
          !
        </div>
        <p className="eyebrow">API unavailable</p>
        <h2>Vehicle Master help could not be opened.</h2>
        <p className="muted-copy">Retry when the FIS API is available.</p>
        <div className="button-row">
          <Link className="button button-primary" href="/vehicles/help">
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

export default async function VehicleHelpPage() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/vehicles/help" />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return <UnavailableState />;
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card legacy-doc" aria-labelledby="vehicle-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Vehicle Master</p>
            <h1 id="vehicle-help-title">Vehicle Master Information / Help</h1>
            <p>Reference information for capturing and maintaining vehicle records.</p>
          </div>
          <Link className="button button-secondary" href="/vehicles">
            Vehicle Master
          </Link>
        </header>

        <section aria-labelledby="vehicle-master-purpose-title">
          <h2 id="vehicle-master-purpose-title">FIS PROGRAM: VEHICLE MASTER FILE</h2>

          <h3>Current Operators</h3>
          <p>1. Sarie Koen - Bedfordview Depot (011) 372-9008 - sariek@gpg.gov.za</p>
          <p>2. Violet Small - Bedfordview Depot (011) 372-9006 - violets@gpg.gov.za</p>

          <h3>Purpose of vehicle master file</h3>
          <ul>
            <li>To capture vehicle data on FIS</li>
            <li>To obtain management reports and discrepancies</li>
            <li>To control the vehicle list</li>
          </ul>

          <h3>Programmer</h3>
          <p>
            <strong>Add a Vehicle:</strong> Louis Coetzee - Directorate Systems
          </p>
          <p>
            <strong>Edit a Vehicle:</strong> Walter Luwes - Directorate Systems (012) 310-2279 - walterl@gpg.gov.za
          </p>
        </section>

        <hr />

        <section aria-labelledby="vehicle-add-help-title">
          <h2 id="vehicle-add-help-title">SCREEN: Add a Vehicle</h2>
          <ol>
            <li>
              <strong>GG Number allocated to vehicle:</strong> Delivery note of vehicle.
            </li>
            <li>
              <strong>Current Registration Number:</strong> If a provincial registration number has been allocated, capture it here. If not, the GG number is also captured here.
            </li>
            <li>
              <strong>Make &amp; Model:</strong> Choose the correct make and model from the drop-down box using the delivery note.
            </li>
            <li>
              <strong>Color:</strong> Choose the color from the delivery or inspection form. If it is not listed, choose “Other”.
            </li>
            <li>
              <strong>Specify Color:</strong> Enter the correct color when “Other” is selected.
            </li>
            <li>
              <strong>Year Manufactured:</strong> Use the year shown on the delivery note.
            </li>
            <li>
              <strong>Tare:</strong> Use the delivery note or registration document.
            </li>
            <li>
              <strong>VIN (Chassis) Number:</strong> Compare the delivery and inspection form with the vehicle at Government Garage.
            </li>
            <li>
              <strong>Engine Number:</strong> Compare the delivery and inspection form with the vehicle.
            </li>
            <li>
              <strong>Take On Odo:</strong> Capture the odometer reading on the date of delivery.
            </li>
            <li>
              <strong>Additional fuel tank:</strong> This is 0 unless an additional fuel tank is fitted.
            </li>
            <li>
              <strong>Take On Date:</strong> The date the vehicle is captured on FIS.
            </li>
            <li>
              <strong>Location:</strong> Select the Government Garage where the vehicle is allocated.
            </li>
            <li>
              <strong>Status:</strong> A newly loaded vehicle starts as <strong>New</strong> and changes as its lifecycle progresses.
            </li>
            <li>
              <strong>Status Date:</strong> The date of the status change.
            </li>
            <li>
              <strong>Hire Type:</strong> Choose the applicable hire type.
            </li>
            <li>
              <strong>Specifications:</strong> Select applicable extras such as radio or air conditioning.
            </li>
            <li>
              <strong>Submit:</strong> Confirm the information before saving it to FIS.
            </li>
          </ol>
        </section>

        <section aria-labelledby="vehicle-edit-help-title">
          <h2 id="vehicle-edit-help-title">SCREEN: Edit a Vehicle</h2>
          <p>
            <strong>Screen 1:</strong> Enter the GG number or registration number and submit it.
          </p>
          <p>
            <strong>Screen 2:</strong> Edit the vehicle information permitted by the workflow.
          </p>
          <ol>
            <li>Only the fields supported by the Vehicle Master maintenance workflow may be changed.</li>
            <li>Review the values before submitting the update.</li>
          </ol>
        </section>
      </article>
    </main>
  );
}
