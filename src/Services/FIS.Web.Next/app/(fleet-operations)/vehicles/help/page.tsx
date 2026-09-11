import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import RouteLoading from "@/components/app-shell/route-loading";
import { getSession } from "@/lib/auth/session";

function UnavailableState() {
  return (
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
  );
}

async function VehicleHelpContent() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath="/vehicles/help" />;
  }

  if (session.status === "unavailable") {
    return <UnavailableState />;
  }

  return (
    <div className="module-help-content">
      <section className="module-help-section" aria-labelledby="vehicle-master-purpose-title">
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
        <p className="module-help-note">
          <strong>Edit a Vehicle:</strong> Walter Luwes - Directorate Systems (012) 310-2279 -
          walterl@gpg.gov.za
        </p>
      </section>

      <section className="module-help-section" aria-labelledby="vehicle-add-help-title">
        <h2 id="vehicle-add-help-title">SCREEN: Add a Vehicle</h2>
        <p className="module-help-intro">
          Capture the vehicle from its delivery and inspection documentation, then submit the
          completed record to FIS.
        </p>
        <ol className="module-help-steps">
          <li>
            <strong>GG Number allocated to vehicle:</strong> Use the delivery note of the vehicle.
          </li>
          <li>
            <strong>Current Registration Number:</strong> If a provincial registration number has
            been allocated, capture it here. If not, capture the GG number here as well.
          </li>
          <li>
            <strong>Make &amp; Model:</strong> Choose the correct make and model from the drop-down
            box using the delivery note. If it is not on the system, contact Rico Hein.
          </li>
          <li>
            <strong>Color:</strong> Choose the color from the delivery or inspection form. If it is
            not listed, choose &ldquo;Other&rdquo;.
          </li>
          <li>
            <strong>Specify Color:</strong> Type the correct color from the delivery or inspection
            form in this block.
          </li>
          <li>
            <strong>Year manufactured:</strong> Use the year shown on the delivery note.
          </li>
          <li>
            <strong>Tare:</strong> Use the delivery note or the vehicle registration document.
          </li>
          <li>
            <strong>VIN (Chassis) Number:</strong> Compare the delivery and inspection form with the
            vehicle at Government Garage.
          </li>
          <li>
            <strong>Engine Number:</strong> Compare the delivery and inspection form with the
            vehicle at Government Garage.
          </li>
          <li>
            <strong>Take on Odo:</strong> Capture the odometer kilometres on the date of delivery
            from the delivery note or inspection form.
          </li>
          <li>
            <strong>Additional fuel tank:</strong> This field is 0 unless the vehicle has an
            additional fuel tank.
          </li>
          <li>
            <strong>Take on date:</strong> The date when the vehicle is captured on FIS.
          </li>
          <li>
            <strong>Location:</strong> Select the Government Garage where the vehicle is allocated:
            Johannesburg or Pretoria. Do not use an unknown location.
          </li>
          <li>
            <strong>Status:</strong> Select the applicable status. The first load starts as
            <strong> New</strong>; when put on contract, change it to <strong>in service</strong>.
          </li>
          <li>
            <strong>Status Date:</strong> The date of the status change. The first date is the
            take-on date and changes whenever the status changes.
          </li>
          <li>
            <strong>Hire Type:</strong> Choose the applicable type, such as Permanent Hire, Hire
            Pool, or VIP Service.
          </li>
          <li>
            <strong>Specifications:</strong> Select applicable extras such as radio or air
            conditioning.
          </li>
          <li>
            <strong>Submit:</strong> When all information is correct, press Submit to save it on
            FIS.
          </li>
        </ol>
      </section>

      <section className="module-help-section" aria-labelledby="vehicle-edit-help-title">
        <h2 id="vehicle-edit-help-title">SCREEN: Edit a Vehicle</h2>
        <p className="module-help-intro">
          Screen 1: Type the GG number or registration number of the vehicle to be edited and press
          Submit.
        </p>
        <details className="module-help-disclosure" open>
          <summary>Screen 2: Edit a vehicle</summary>
          <ol className="module-help-steps">
            <li>
              Only the current GG number, current registration number, color, tare, location,
              status, status changed date, hire type, comment, extras, engine number, and chassis
              number can be changed. A stolen status must be changed using the Losses screen.
            </li>
            <li>Click the relevant block and change the information that needs updating.</li>
          </ol>
        </details>
      </section>
    </div>
  );
}

export default function VehicleHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card module-help-page" aria-labelledby="vehicle-help-title">
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
        <Suspense fallback={<RouteLoading />}>
          <VehicleHelpContent />
        </Suspense>
      </article>
    </main>
  );
}
