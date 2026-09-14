import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import {
  FinanceFrame,
  FinanceRestricted,
  FinanceUnavailable,
} from "@/app/(fleet-operations)/finance/_components";
import { hasFinanceRole } from "@/app/(fleet-operations)/finance/_utils";
import { getSession } from "@/lib/auth/session";

type Query = Record<string, string | string[] | undefined>;

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function positiveInteger(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
}

export default async function JournalInvoicePage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <FinanceFrame
        title="Detailed Invoice from Journal Number"
        description="Enter a journal number to view its detailed transactions."
      >
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasFinanceRole(session.roles))
    return (
      <FinanceFrame
        title="Detailed Invoice from Journal Number"
        description="Enter a journal number to view its detailed transactions."
      >
        <FinanceRestricted />
      </FinanceFrame>
    );

  const query = await searchParams;
  const journalNumber = positiveInteger(queryValue(query, "journalNumber"));
  const submitted = queryValue(query, "run") === "1";
  if (submitted && journalNumber) {
    redirect(
      `/finance/reports/output?${new URLSearchParams({
        kind: "legacy-detail",
        item: "journal-detailed-invoice",
        journalNumber: String(journalNumber),
        format: "html",
      }).toString()}`,
    );
  }

  return (
    <FinanceFrame
      title="Detailed Invoice from Journal Number"
      description="Enter the journal number for the detailed transactions report retained from Finance."
    >
      {submitted ? (
        <div className="notice notice-error" role="alert">
          Enter a valid journal number.
        </div>
      ) : null}
      <form className="vehicle-status-maintenance-panel" method="get">
        <input name="run" type="hidden" value="1" />
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="journal-invoice-number">
              Journal Number
            </label>
            <input
              className="form-input"
              id="journal-invoice-number"
              name="journalNumber"
              inputMode="numeric"
              defaultValue={queryValue(query, "journalNumber")}
              required
            />
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Submit Details
          </button>
          <Link className="button button-secondary" href="/finance">
            Back
          </Link>
        </div>
      </form>
    </FinanceFrame>
  );
}
