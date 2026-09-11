import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { deleteModelAction } from "@/app/(administration)/validation-data/models/actions";
import { getModel, getModelDeleteCheck, ModelApiError } from "@/lib/api/reference-data/api-models";
import { getSession } from "@/lib/auth/session";

type ModelDeleteCheckPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function parseCode(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 && parsed <= 32767 ? parsed : null;
}

function ErrorCard({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Model deletion</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_model.aspx">
        Model Maintenance
      </Link>
    </section>
  );
}

async function renderModelDeleteCheckPage({ searchParams }: ModelDeleteCheckPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Model_Del_Check.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Model deletion is temporarily unavailable." />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to delete vehicle models." />
      </main>
    );

  const query = await searchParams;
  const modelCode = parseCode(getQueryValue(query.code) ?? getQueryValue(query.modelCode));
  const error = getQueryValue(query.error);
  if (!modelCode)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Select a model before attempting deletion." />
      </main>
    );
  if (error)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message={error} />
      </main>
    );

  try {
    const [model, dependencies] = await Promise.all([
      getModel(modelCode),
      getModelDeleteCheck(modelCode),
    ]);
    if (!model)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Model ${modelCode} was not found.`} />
        </main>
      );
    const blocked = dependencies.vehicleCount > 0;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="model-delete-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Vehicle</p>
              <h1 id="model-delete-title">Delete Model</h1>
              <p>Check linked vehicles before deleting this model.</p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_model.aspx">
              Model Maintenance
            </Link>
          </header>
          <section className="vehicle-status-card" role={blocked ? "alert" : "note"}>
            <p className="eyebrow">Model {model.modelCode}</p>
            <h2>{model.modelDescription}</h2>
            <dl className="status-maintenance-details">
              <div>
                <dt>Linked vehicles</dt>
                <dd>{dependencies.vehicleCount}</dd>
              </div>
            </dl>
            {blocked ? (
              <>
                <p className="muted-copy">
                  Please move or remove the linked vehicles before deleting this model.
                </p>
                <Link className="button button-secondary" href="/Validation/MNT_model.aspx">
                  Return to Model Maintenance
                </Link>
              </>
            ) : (
              <form action={deleteModelAction} className="button-row">
                <input name="modelCode" type="hidden" value={model.modelCode} readOnly />
                <button className="button button-primary" type="submit">
                  Confirm Delete
                </button>
                <Link className="button button-secondary" href="/Validation/MNT_model.aspx">
                  Cancel
                </Link>
              </form>
            )}
          </section>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof ModelApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`/Validation/MNT_Model_Del_Check.aspx?code=${modelCode}`} />
        </main>
      );
    if (error instanceof ModelApiError && error.status === 404)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Model ${modelCode} was not found.`} />
        </main>
      );
    console.error(
      "FIS model delete check failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Model dependencies could not be checked." />
      </main>
    );
  }
}

export default function ModelDeleteCheckPage(props: ModelDeleteCheckPageProps) {
  return <Suspense fallback={<RouteLoading />}>{renderModelDeleteCheckPage(props)}</Suspense>;
}
