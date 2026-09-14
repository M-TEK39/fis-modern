import type { ReactNode } from "react";

import GovernmentReportLetterhead from "@/components/ui/government-report-letterhead";
import ReportPrintButton from "@/components/ui/report-print-button";

type ReportResultsPanelProps = Readonly<{
  headingId: string;
  eyebrow?: ReactNode;
  heading: ReactNode;
  trailing?: ReactNode;
  widePrintLayout?: boolean;
  printOrientation?: "portrait" | "landscape";
  printButton?: boolean;
  letterheadTitle?: string;
  letterheadGovernmentMotorTransport?: boolean;
  children: ReactNode;
}>;

export default function ReportResultsPanel({
  headingId,
  eyebrow = "Report results",
  heading,
  trailing,
  widePrintLayout = false,
  printOrientation = "landscape",
  printButton = true,
  letterheadTitle,
  letterheadGovernmentMotorTransport = false,
  children,
}: ReportResultsPanelProps) {
  return (
    <section
      className={`vehicle-status-maintenance-panel report-print-area report-print-area--${printOrientation}${
        widePrintLayout ? " report-print-area--wide" : ""
      }`}
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
          {printButton ? <ReportPrintButton /> : null}
        </div>
      </div>
      {children}
    </section>
  );
}
