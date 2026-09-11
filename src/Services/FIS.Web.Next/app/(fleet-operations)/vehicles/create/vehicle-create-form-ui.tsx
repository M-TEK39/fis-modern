import type { ReactNode } from "react";
import { useFormStatus } from "react-dom";

export function VehicleCreateField({
  id,
  label,
  required = false,
  children,
}: Readonly<{ id: string; label: string; required?: boolean; children: ReactNode }>) {
  return (
    <div className="field">
      <label htmlFor={id}>
        {label} {required ? <span aria-hidden="true">*</span> : null}
        {required ? <span className="sr-only"> required</span> : null}
      </label>
      {children}
    </div>
  );
}

export function VehicleCreateSubmitButton() {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Submitting..." : "Submit for authorization"}
    </button>
  );
}
