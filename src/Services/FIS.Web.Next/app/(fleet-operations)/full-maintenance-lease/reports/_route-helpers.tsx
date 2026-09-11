import {
  ApiUnavailable,
  FmlFrame,
} from "@/app/(fleet-operations)/full-maintenance-lease/_components";

export function reportError(error: unknown, message: string) {
  return (
    <FmlFrame title="FML Report" description="The report could not be loaded.">
      <ApiUnavailable
        message={error instanceof Error && error.name === "FmlApiError" ? error.message : message}
      />
    </FmlFrame>
  );
}
