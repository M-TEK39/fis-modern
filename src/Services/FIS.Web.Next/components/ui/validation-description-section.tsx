import type { ReactNode } from "react";

type ValidationDescriptionSectionProps = Readonly<{
  headingId: string;
  eyebrow: string;
  heading: string;
  label: string;
  value: string | null | undefined;
  note: ReactNode;
}>;

export default function ValidationDescriptionSection({
  headingId,
  eyebrow,
  heading,
  label,
  value,
  note,
}: ValidationDescriptionSectionProps) {
  return (
    <section className="vehicle-form-section" aria-labelledby={headingId}>
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{eyebrow}</p>
          <h2 id={headingId}>{heading}</h2>
        </div>
        <span className="vehicle-required-note">* Required</span>
      </div>
      <div className="field-grid">
        <div className="field">
          <label htmlFor="description">
            {label} <span aria-hidden="true">*</span>
            <span className="sr-only"> required</span>
          </label>
          <input
            id="description"
            name="description"
            type="text"
            maxLength={30}
            defaultValue={value ?? ""}
            required
          />
        </div>
      </div>
      <p className="muted-copy">{note}</p>
    </section>
  );
}
