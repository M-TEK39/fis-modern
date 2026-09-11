"use client";

import { useFormStatus } from "react-dom";

export default function StatusMaintenanceStatusSubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Updating..." : "Update Status"}
    </button>
  );
}
