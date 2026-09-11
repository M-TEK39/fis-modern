import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import {
  FinanceFrame,
  FinanceMenuLink,
  FinanceMenuSection,
  FinanceRestricted,
  FinanceUnavailable,
} from "@/app/(fleet-operations)/finance/_components";
import { hasFinanceRole } from "@/app/(fleet-operations)/finance/_utils";
import {
  FinanceApiError,
  getBatchStatus,
  type FinanceBatchStatus,
} from "@/lib/api/finance/api-finance";
import { getSession } from "@/lib/auth/session";

import { runBatchAction } from "./actions";

type Query = Record<string, string | string[] | undefined>;
type BatchPageProps = { searchParams?: Promise<Query>; action?: string };

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function dateInput() {
  return new Date().toISOString().slice(0, 10);
}

function actionLabel(action: string) {
  return action === "start"
    ? "Start batch process and take site offline"
    : action === "check-scoa"
      ? "Check the SCOA version and update where needed"
      : action === "rollback"
        ? "Roll back batch"
        : "Finish batch process and bring site back online";
}

const BatchManagementContent = renderBatchManagementContent;

async function renderBatchManagementContent({ searchParams, action }: BatchPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <FinanceFrame title="Batch Management" description="Batch processing management.">
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasFinanceRole(session.roles))
    return (
      <FinanceFrame title="Batch Management" description="Batch processing management.">
        <FinanceRestricted />
      </FinanceFrame>
    );

  const query = searchParams ? await searchParams : {};
  let batch: FinanceBatchStatus | null = null;
  try {
    batch = await getBatchStatus();
  } catch (error) {
    if (!(error instanceof FinanceApiError)) throw error;
  }
  const normalizedAction = action?.trim().toLowerCase() ?? "";
  const result = queryValue(query, "result");
  const message = queryValue(query, "message");
  const actionIsKnown = ["start", "check-scoa", "rollback", "finish"].includes(normalizedAction);
  const hasError = result === "error" || result === "forbidden";

  return (
    <FinanceFrame title="Batch Management" description="Batch processing management.">
      {batch ? (
        <div className="notice notice-info" role="status">
          Batch status: {batch.isActive ? "Running" : "Not running"}
          {batch.status ? ` (${batch.status})` : ""}
        </div>
      ) : (
        <FinanceUnavailable message="Batch status could not be loaded. The Finance API will still enforce batch rules." />
      )}
      {message ? (
        <div
          className={`notice ${hasError ? "notice-error" : "notice-success"}`}
          role={hasError ? "alert" : "status"}
        >
          {message}
        </div>
      ) : null}
      {actionIsKnown ? (
        <section className="vehicle-status-maintenance-panel" aria-labelledby="batch-action-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Batch action</p>
              <h2 id="batch-action-title">{actionLabel(normalizedAction)}</h2>
            </div>
          </div>
          {normalizedAction === "start" ? (
            <form action={runBatchAction}>
              <input name="action" type="hidden" value="start" />
              <div className="field-grid">
                <div className="field">
                  <label htmlFor="batch-date">Batch Date</label>
                  <input
                    id="batch-date"
                    name="batchDate"
                    type="date"
                    defaultValue={dateInput()}
                    required
                  />
                </div>
              </div>
              <div className="button-row">
                <button className="button button-primary" type="submit">
                  Start Batch
                </button>
                <Link className="button button-secondary" href="/finance/batch-management">
                  Cancel
                </Link>
              </div>
            </form>
          ) : (
            <form action={runBatchAction}>
              <input name="action" type="hidden" value={normalizedAction} />
              <p className="muted-copy">
                This operation uses the existing Finance batch API and keeps its legacy database
                workflow.
              </p>
              <div className="button-row">
                <button className="button button-primary" type="submit">
                  {normalizedAction === "check-scoa"
                    ? "Run SCOA Check"
                    : normalizedAction === "rollback"
                      ? "Roll Back Batch"
                      : "Finish Batch"}
                </button>
                <Link className="button button-secondary" href="/finance/batch-management">
                  Cancel
                </Link>
              </div>
            </form>
          )}
        </section>
      ) : (
        <div className="vehicle-menu-tiles">
          <FinanceMenuSection title="Batch Management">
            <FinanceMenuLink href="/finance/batch-management/start">
              Start batch process and take site offline
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance">Access Finance menu</FinanceMenuLink>
            <FinanceMenuLink href="/finance/batch-management/check-scoa">
              Check the SCOA version and update where needed
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/batch-management/rollback">
              Roll back batch
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/batch-management/finish">
              Finish batch process and bring site back online
            </FinanceMenuLink>
          </FinanceMenuSection>
        </div>
      )}
    </FinanceFrame>
  );
}

export function BatchManagementRoute(props: BatchPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <BatchManagementContent {...props} />
    </Suspense>
  );
}

export default function BatchManagementPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  return <BatchManagementRoute searchParams={searchParams} />;
}
