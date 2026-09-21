"use client";

const PRINT_CONFIRM =
  "Printing of Vehicle details and inception into the database. Select OK to Proceed or Cancel to go back.";

export default function AuthorizedPrintButton({
  id,
  returnPath = "/vehicles/create",
}: Readonly<{ id: number; returnPath?: string }>) {
  return (
    <button
      className="button button-primary button-small"
      type="button"
      onClick={() => {
        if (!window.confirm(PRINT_CONFIRM)) {
          return;
        }

        const params = new URLSearchParams({
          id: String(id),
          returnPath,
        });
        window.location.assign(`/vehicles/authorize/print?${params.toString()}`);
      }}
    >
      Print
    </button>
  );
}
