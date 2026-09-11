"use client";

import { useFormStatus } from "react-dom";

export default function StatusMaintenanceSearchSubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Searching..." : "Submit"}
    </button>
  );
}
