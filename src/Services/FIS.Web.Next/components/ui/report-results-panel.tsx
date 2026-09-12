import type { ReactNode } from "react";

import GovernmentReportLetterhead from "@/components/ui/government-report-letterhead";
import ReportPrintButton from "@/components/ui/report-print-button";

type ReportResultsPanelProps = Readonly<{
  headingId: string;
  eyebrow?: ReactNode;
  heading: ReactNode;
  trailing?: ReactNode;
  letterheadTitle?: string;
  letterheadGovernmentMotorTransport?: boolean;
  children: ReactNode;
}>;

export default function ReportResultsPanel({
  headingId,
  eyebrow = "Report results",
  heading,
  trailing,
  letterheadTitle,
  letterheadGovernmentMotorTransport = false,
  children,
}: ReportResultsPanelProps) {
  return (
    <section
      className="vehicle-status-maintenance-panel report-print-area"
      aria-labelledby={headingId}
    >
      <GovernmentReportLetterhead
        governmentMotorTransport={letterheadGovernmentMotorTransport}
        printOnly
        title={letterheadTitle}
      />
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{eyebrow}</p>
          <h2 id={headingId}>{heading}</h2>
        </div>
        <div className="button-row report-print-hide">
          {trailing}
          <ReportPrintButton />
        </div>
      </div>
      {children}
    </section>
  );
}
