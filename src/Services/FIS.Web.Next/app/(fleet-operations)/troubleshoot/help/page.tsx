import Link from "next/link";

export default function TroubleshootHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card module-help-page" aria-labelledby="troubleshoot-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Troubleshoot maintenance</p>
            <h1 id="troubleshoot-help-title">Troubleshoot Maintenance Information / Help</h1>
            <p>Troubleshoot workflow guidance and references.</p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>

        <div className="module-help-content">
          <section className="module-help-section" aria-labelledby="troubleshoot-help-workflow">
            <h2 id="troubleshoot-help-workflow">Troubleshoot workflow</h2>
            <p className="module-help-intro">
              Use Troubleshoot Log to review entries for a department/site, General Reports to
              filter logged problems, and ODOMeter Corrections to locate the current and last
              recorded odometer values.
            </p>
            <p>
              Vehicle Master Edit searches the existing vehicle master record. Recovered vehicles
              continue through the original recovered-GG transaction so the stolen row, replacement
              row, and history remain consistent.
            </p>
            <div className="button-row">
              <Link className="button button-secondary" href="/troubleshoot">
                Troubleshoot menu
              </Link>
            </div>
          </section>

          <section className="module-help-section" aria-labelledby="troubleshoot-help-purpose">
            <h2 id="troubleshoot-help-purpose">Purpose of Program</h2>
            <p className="module-help-intro">
              The purpose of the Troubleshoot program is to provide a uniform program for data
              capturing and correction related to errors made on the ELS and the subsequent analysis
              of the outcome of the matters.
            </p>
          </section>

          <section className="module-help-section" aria-labelledby="troubleshoot-help-analysis">
            <h2 id="troubleshoot-help-analysis">Term / Field Analysis</h2>
            <p>
              The required data fields are mostly self-explanatory, but the definitions below
              specify their intended meaning.
            </p>
            <p className="module-help-note">
              It is assumed that each user of the GGMT administrative functions on the Fleet
              Information System has received on-the-job training for the field relevant to their
              division. Fields seen on more than one page are not duplicated where their meaning
              remains the same.
            </p>
            <details className="module-help-disclosure" open>
              <summary>Field definitions</summary>
              <ol className="module-help-steps">
                <li>
                  <strong>Select Department Site:</strong> Select the relevant users from the
                  specified site.
                </li>
                <li>
                  <strong>Update Logs:</strong> Update a previously registered fault report.
                </li>
                <li>
                  <strong>Reports Menu:</strong> Select to view reports related to faults.
                </li>
                <li>
                  <strong>Home:</strong> Go back to the home page.
                </li>
                <li>
                  <strong>Next:</strong> Enter the next page with the information already supplied.
                </li>
                <li>
                  <strong>Select Problem Type:</strong> Select the type of problem faced.
                </li>
                <li>
                  <strong>Name:</strong> Name of the individual identified on the first page.
                </li>
                <li>
                  <strong>Tel.:</strong> Contact telephone number for the person reporting the
                  fault.
                </li>
                <li>
                  <strong>Email:</strong> Email of the user who reports the fault.
                </li>
                <li>
                  <strong>Log Date:</strong> Date the matter is logged.
                </li>
                <li>
                  <strong>Log time:</strong> Time the matter has been logged on the system.
                </li>
                <li>
                  <strong>Back:</strong> Return to the previous page without saving information.
                </li>
                <li>
                  <strong>Solved?:</strong> Indicate whether the matter has been solved by selecting
                  Y for Yes or N for No in the area where the problem occurred.
                </li>
                <li>
                  <strong>Contract:</strong> Faults related to the areas stipulated under contracts.
                </li>
                <li>
                  <strong>Information:</strong> Problems related to the areas stipulated under
                  Information.
                </li>
                <li>
                  <strong>Actual Error Message:</strong> Description of the error message issued by
                  FIS.
                </li>
                <li>
                  <strong>Other:</strong> Faults that cannot be itemized under another problem
                  identification.
                </li>
                <li>
                  <strong>Additional Comments:</strong> Additional information related to this error
                  log.
                </li>
                <li>
                  <strong>Log:</strong> Submit the information to the database to issue a unique,
                  traceable number.
                </li>
                <li>
                  <strong>Troubleshoot Log:</strong> Log faults and errors on the system.
                </li>
                <li>
                  <strong>Troubleshoot General Reports:</strong> Reports related to errors.
                </li>
                <li>
                  <strong>Odometer Corrections:</strong> Correct incorrect odometer readings that
                  fall within the jurisdiction of the call centre operator.
                </li>
                <li>
                  <strong>Search Criteria:</strong> Identify the vehicle that has to be corrected.
                </li>
                <li>
                  <strong>Submit Details:</strong> Start searching the database.
                </li>
                <li>
                  <strong>Trip Auth:</strong> Trip authority number.
                </li>
                <li>
                  <strong>Start Odo:</strong> Odometer reading at the start of the trip.
                </li>
                <li>
                  <strong>End Odo:</strong> Odometer reading at the end of the trip.
                </li>
                <li>
                  <strong>Distance Traveled:</strong> Distance the vehicle has travelled on the
                  specific route.
                </li>
                <li>
                  <strong>To Correct a Trip Authority:</strong> Select the relevant Trip Authority
                  that needs to be corrected. Limited corrections are allowed; escalate matters
                  outside the allowed correction access to the Consulting Firm Gess.
                </li>
                <li>
                  <strong>If the matter can be corrected:</strong> Change Start Odo allows the start
                  and end odometer of the previous Trip Authority to be changed; Close Open Trip
                  Authority closes an open authority; Open Closed Trip Authority opens an incorrect
                  authority to correct the odometer.
                </li>
              </ol>
            </details>
          </section>
        </div>
      </article>
    </main>
  );
}
