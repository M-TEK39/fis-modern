import { useFormStatus } from "react-dom";

import type { VehicleDetailActionState } from "@/app/(fleet-operations)/vehicles/[vmfCode]/actions";

export function VehicleDetailActionNotice({
  state,
}: Readonly<{ state: VehicleDetailActionState }>) {
  if (state.status === "idle" || !state.message) return null;
  return (
    <div
      className={`notice ${state.status === "error" ? "notice-error" : "notice-success"}`}
      role={state.status === "error" ? "alert" : "status"}
    >
      <span aria-hidden="true">{state.status === "error" ? "!" : "✓"}</span>
      <span>{state.message}</span>
    </div>
  );
}

export function VehicleDetailSubmitButton({
  label,
  pendingLabel,
}: Readonly<{ label: string; pendingLabel: string }>) {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary button-small" type="submit" disabled={pending}>
      {pending ? pendingLabel : label}
    </button>
  );
}

export function VehicleDocumentDeleteButton() {
  const { pending } = useFormStatus();
  return (
    <button className="button button-danger button-small" type="submit" disabled={pending}>
      {pending ? "Deleting..." : "Delete"}
    </button>
  );
}
