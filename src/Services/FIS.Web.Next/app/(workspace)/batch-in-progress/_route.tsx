import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";
import { logoutAction } from "@/app/(auth)/actions/auth";
import { hasAdvancedBatchOperationsRole } from "@/app/(fleet-operations)/finance/_utils";
import {
  FinanceApiError,
  getBatchStatus,
  type FinanceBatchStatus,
} from "@/lib/api/finance/api-finance";
import { getSession } from "@/lib/auth/session";
import { redirect } from "next/navigation";
import { connection } from "next/server";

function formatArchiveBatchWhen(value: string | null) {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime()) || date.getFullYear() < 2) return "";
  const day = String(date.getDate()).padStart(2, "0");
  const month = date.toLocaleDateString("en-GB", { month: "long" });
  const year = String(date.getFullYear());
  const hour12 = date.getHours() % 12 || 12;
  const minutes = String(date.getMinutes()).padStart(2, "0");
  return `${day} ${month} ${year} : ${String(hour12).padStart(2, "0")}:${minutes}`;
}

function batchInProgressCopy(batch: FinanceBatchStatus, advanced: boolean) {
  if (batch.jobStatusError) return "error" as const;
  if (!batch.isActive) return 0;
  if (advanced && batch.rollbackOperationsActive) return 3;
  if (advanced && batch.databaseOperationsActive) return 2;
  if (advanced) return "complete" as const;
  return 1;
}

async function BatchInProgressContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="batch-in-progress-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Batch in progress</p>
              <h1 id="batch-in-progress-title">Batch in progress</h1>
            </div>
          </header>
          <section className="vehicle-status-card" role="alert">
            <p className="eyebrow">Service unavailable</p>
            <h2>Your session could not be checked.</h2>
            <p className="muted-copy">
              The sign-in service is temporarily unavailable. Please try again.
            </p>
          </section>
        </section>
      </main>
    );
  }

  let batch: FinanceBatchStatus | null = null;
  try {
    batch = await getBatchStatus();
  } catch (error) {
    if (!(error instanceof FinanceApiError)) throw error;
  }

  if (!batch) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="batch-in-progress-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Batch in progress</p>
              <h1 id="batch-in-progress-title">Batch in progress</h1>
            </div>
          </header>
          <section className="vehicle-status-card" role="alert">
            <p className="eyebrow">Service unavailable</p>
            <h2>Batch status could not be loaded.</h2>
            <p className="muted-copy">Retry when the Finance API is available.</p>
          </section>
        </section>
      </main>
    );
  }

  const advanced = hasAdvancedBatchOperationsRole(session.roles);
  const status = batchInProgressCopy(batch, advanced);
  if (status === "error") {
    const params = new URLSearchParams();
    params.set("result", "error");
    params.set("message", batch.jobStatusError ?? "Batch job status could not be read.");
    redirect(`/finance/batch-management/rollback?${params.toString()}`);
  }
  if (status === "complete") redirect("/finance/batch-management");

  const started = formatArchiveBatchWhen(batch.jobStartDate);
  const rollbackStarted = formatArchiveBatchWhen(batch.rollbackStartDate);
  const hours = batch.typicalHours ?? 4;
  const latestStep = batch.latestLog?.trim() || "unavailable";

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="batch-in-progress-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Batch in progress</p>
            <h1 id="batch-in-progress-title">Batch in progress</h1>
            <p>Site availability while a finance batch is running.</p>
          </div>
        </header>
        <section className="vehicle-status-card" role="status">
          {status === 0 ? (
            <>
              <p className="eyebrow">No batch</p>
              <h2>There is currently no batch in progress.</h2>
              <p className="muted-copy">Normal operations can proceed.</p>
            </>
          ) : null}
          {status === 1 ? (
            <>
              <p className="eyebrow">Batch running</p>
              <h2>There is currently a batch run in progress.</h2>
              <p className="muted-copy">
                The batch operation was started on <strong>{started || "an unknown time"}</strong>{" "}
                and usually takes <strong>{hours}</strong> hours to complete.
              </p>
            </>
          ) : null}
          {status === 2 ? (
            <>
              <p className="eyebrow">Database operations</p>
              <h2>The batch is currently running database operations.</h2>
              <p className="muted-copy">
                Please note this will be the only screen available until the database operations
                complete.
              </p>
              <p className="muted-copy">
                The operation was started on <strong>{started || "an unknown time"}</strong> and
                usually takes <strong>{hours}</strong> hours to complete.
              </p>
              <p className="muted-copy">
                The latest step completed by the batch scripts was <strong>{latestStep}</strong>.
              </p>
            </>
          ) : null}
          {status === 3 ? (
            <>
              <p className="eyebrow">Rollback</p>
              <h2>The batch is currently being rolled back.</h2>
              <p className="muted-copy">
                Please note this will be the only screen available until the rollback completes.
              </p>
              <p className="muted-copy">
                The rollback was started on{" "}
                <strong>{rollbackStarted || "an unknown time"}</strong> and usually takes{" "}
                <strong>1</strong> hours to complete.
              </p>
            </>
          ) : null}
        </section>
        <div className="vehicle-footer-actions">
          <form action={logoutAction}>
            <button className="button button-secondary" type="submit">
              Logout
            </button>
          </form>
        </div>
      </section>
    </main>
  );
}

export function BatchInProgressRoute() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <BatchInProgressContent />
    </Suspense>
  );
}
