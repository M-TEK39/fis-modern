import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import {
  FinanceFrame,
  FinanceRestricted,
  FinanceUnavailable,
  hasFinanceRole,
} from "@/app/finance/_components";
import { FinanceApiError, getFinanceBatchDates, type FinanceOption } from "@/lib/api-finance";
import { getSession } from "@/lib/session";

type Query = Record<string, string | string[] | undefined>;
type InterfaceAction = "pastel-csv" | "pastel-csv-customer";

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function titleFor(action: string) {
  return action === "pastel-csv-customer"
    ? "Download Pastel CSV Interface File with Customer Column"
    : "Download Pastel CSV Interface File";
}

function outputHref(action: InterfaceAction, batchDate: string) {
  const params = new URLSearchParams({ kind: "interface", action, batchDate });
  return `/finance/reports/output?${params.toString()}`;
}

function optionList(options: FinanceOption[]) {
  return (
    <>
      <option value="">Select Batch Date</option>
      {options.map((item) => (
        <option key={item.value} value={item.value}>
          {item.label}
        </option>
      ))}
    </>
  );
}

export default async function FinanceInterfacePage({
  params,
  searchParams,
}: Readonly<{ params: Promise<{ action: string }>; searchParams: Promise<Query> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <FinanceFrame
        title="GPG-SAP Interface"
        description="Interface file generation and downloads."
      >
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasFinanceRole(session.roles))
    return (
      <FinanceFrame
        title="GPG-SAP Interface"
        description="Interface file generation and downloads."
      >
        <FinanceRestricted />
      </FinanceFrame>
    );

  const { action } = await params;
  const normalizedAction = action.trim().toLowerCase();
  const query = await searchParams;
  const validAction =
    normalizedAction === "pastel-csv" || normalizedAction === "pastel-csv-customer";
  let batchDates: FinanceOption[] = [];
  let error: string | null = validAction
    ? null
    : "The requested interface action is not available.";
  try {
    if (validAction) batchDates = await getFinanceBatchDates();
  } catch (caught) {
    error =
      caught instanceof FinanceApiError
        ? caught.message
        : "Interface batch dates could not be loaded.";
  }

  const batchDate = queryValue(query, "batchDate");
  const submitted = queryValue(query, "run") === "1";
  const output =
    submitted && validAction && batchDate
      ? outputHref(normalizedAction as InterfaceAction, batchDate)
      : null;
  if (submitted && validAction && !batchDate)
    error = "Select a batch date before generating the interface file.";

  return (
    <FinanceFrame
      title={titleFor(normalizedAction)}
      description="Interface file generation and downloads."
    >
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      <form className="vehicle-status-maintenance-panel" method="get">
        <input name="run" type="hidden" value="1" />
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="interface-batch-date">
              Batch Date
            </label>
            <select
              className="form-select"
              id="interface-batch-date"
              name="batchDate"
              defaultValue={batchDate}
              required
            >
              {optionList(batchDates)}
            </select>
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Generate Interface File
          </button>
          <Link className="button button-secondary" href="/finance">
            Finance Menu
          </Link>
        </div>
      </form>
      {output ? (
        <section className="vehicle-status-maintenance-panel" aria-labelledby="interface-output">
          <h2 id="interface-output">Interface file ready</h2>
          <p className="muted-copy">The file is generated on the authenticated server path.</p>
          <a className="button button-primary" href={output}>
            Download interface file
          </a>
        </section>
      ) : null}
    </FinanceFrame>
  );
}
