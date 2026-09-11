import type { ReactNode } from "react";
import { useFormStatus } from "react-dom";

export function VehicleEditField({
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

export function VehicleEditFieldError({ message }: Readonly<{ message?: string }>) {
  return message ? (
    <span className="muted-copy" role="alert">
      {message}
    </span>
  ) : null;
}

export function VehicleEditSubmitButton() {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Updating..." : "Update vehicle"}
    </button>
  );
}
