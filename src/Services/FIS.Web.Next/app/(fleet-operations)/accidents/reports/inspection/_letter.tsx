import Link from "next/link";

import {
  AccidentLetterClosing,
  AccidentLetterHeader,
  AccidentReportFooter,
} from "@/app/(fleet-operations)/accidents/reports/_report-components";
import PrintButton from "@/app/(fleet-operations)/accidents/reports/print-button";
import type { AccidentOutstandingDocumentReport } from "@/lib/api/fleet-operations/api-accidents";

function valueOrDash(value: string | null) {
  return value?.trim() || "-";
}

function LetterAddress({ report }: { report: AccidentOutstandingDocumentReport }) {
  const lines = [report.address1, report.address2, report.postalCode].filter(
    (line): line is string => Boolean(line?.trim()),
  );
  return (
    <address>
      {lines.length > 0 ? (
        lines.map((line, index) => <div key={`${index}-${line}`}>{line}</div>)
      ) : (
        <div>-</div>
      )}
    </address>
  );
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

export default function InspectionLetter({
  report,
}: Readonly<{ report: AccidentOutstandingDocumentReport }>) {
  const fleetNumber = report.fleetNumber?.trim() || "";
  const ggReference = report.ggReference?.trim() || "";
  const reference = fleetNumber || ggReference ? `${fleetNumber}-${ggReference}` : "-";
  return (
    <article className="vehicle-card accident-letter" aria-labelledby="inspection-letter-title">
      <header className="vehicle-page-header">
        <div>
          <p className="eyebrow">Accident report letter</p>
          <h1 id="inspection-letter-title">INSPECTION LETTER</h1>
        </div>
        <div className="button-row">
          <Link className="button button-secondary" href="/accidents/reports/inspection">
            Back
          </Link>
          <PrintButton />
        </div>
      </header>
      <AccidentLetterHeader reference={reference} />
      <section className="vehicle-status-maintenance-panel">
        <h2>The Transport Manager</h2>
        <p>
          <strong>
            {valueOrDash(report.departmentNumber)} - {valueOrDash(report.siteDescription)}
          </strong>
        </p>
        <LetterAddress report={report} />
        <dl className="contract-facts">
          <div>
            <dt>CONTACT PERSON</dt>
            <dd>{valueOrDash(report.responsiblePerson)}</dd>
          </div>
          <div>
            <dt>PHONE / MOBILE NO</dt>
            <dd>{valueOrDash(report.telephone)}</dd>
          </div>
          <div>
            <dt>FAX NO</dt>
            <dd>{valueOrDash(report.fax)}</dd>
          </div>
          <div>
            <dt>REG. NO</dt>
            <dd>{valueOrDash(report.registrationNumber)}</dd>
          </div>
        </dl>
      </section>
      <section className="vehicle-status-maintenance-panel">
        <p>The above-mentioned incident refers.</p>
        <p>
          Kindly bring vehicle, <strong>{valueOrDash(report.registrationNumber)}</strong>, for
          inspection and if deemed necessary repair of the vehicle.
        </p>
        <p>
          Kindly take note that should the vehicle be involved in another accident and the damage
          cannot be separated, your Department will be held liable for the cost.
        </p>
        <p>This letter must accompany the vehicle if brought in for inspection.</p>
      </section>
      <AccidentLetterClosing />
      <AccidentReportFooter
        includeAccidentMenu={false}
        reportMenuHref="/accidents/reports/inspection"
      />
    </article>
  );
}
