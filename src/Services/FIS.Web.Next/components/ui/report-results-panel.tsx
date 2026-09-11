import type { ReactNode } from "react";

type ReportResultsPanelProps = Readonly<{
  headingId: string;
  eyebrow?: ReactNode;
  heading: ReactNode;
  trailing?: ReactNode;
  children: ReactNode;
}>;

export default function ReportResultsPanel({
  headingId,
  eyebrow = "Report results",
  heading,
  trailing,
  children,
}: ReportResultsPanelProps) {
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby={headingId}>
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{eyebrow}</p>
          <h2 id={headingId}>{heading}</h2>
        </div>
        {trailing}
      </div>
      {children}
    </section>
  );
}
