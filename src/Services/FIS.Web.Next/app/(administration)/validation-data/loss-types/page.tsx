import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { logoutAction } from "@/app/(auth)/actions/auth";
import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { createLossTypeAction } from "@/app/(administration)/validation-data/loss-types/actions";
import LossTypeForm from "@/app/(administration)/validation-data/loss-types/loss-type-form";
import {
  getLossTypes,
  LossTypeApiError,
  type LossTypeRecord,
} from "@/lib/api/fleet-operations/api-loss-types";
import { getSession } from "@/lib/auth/session";

type LossTypeListPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function editPath(lossTypeCode: number) {
  return `/Validation/MNT_Loss_Type_Edit.aspx?cmbLoss=${encodeURIComponent(String(lossTypeCode))}`;
}

function deleteCheckPath(lossTypeCode: number) {
  return `/Validation/MNT_Loss_Type_Del_Check.aspx?code=${encodeURIComponent(String(lossTypeCode))}`;
}

function ErrorCard({
  message,
  routePath = "/validation-data/loss-types",
}: Readonly<{ message: string; routePath?: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Loss description maintenance</p>
      <h2>{message}</h2>
      <div className="button-row">
        <Link className="button button-primary" href={routePath}>
          Try again
        </Link>
        <Link className="button button-secondary" href="/validation-data">
          Validation Data
        </Link>
      </div>
    </section>
  );
}

function LossTypeTable({ lossTypes }: Readonly<{ lossTypes: LossTypeRecord[] }>) {
  if (lossTypes.length === 0)
    return (
      <div className="empty-state">
        <h2>No loss descriptions found</h2>
        <p>Add a loss description using the same legacy validation workflow.</p>
      </div>
    );

  return (
    <div className="table-container">
      <div className="table-header">
        <span className="table-title">
          {lossTypes.length} loss description{lossTypes.length === 1 ? "" : "s"}
        </span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <caption className="sr-only">Loss descriptions</caption>
          <thead>
            <tr>
              <th scope="col">Loss type code</th>
              <th scope="col">Loss description</th>
              <th scope="col">Last updated</th>
              <th scope="col">Actions</th>
            </tr>
          </thead>
          <tbody>
            {lossTypes.map((lossType) => (
              <tr key={lossType.lossTypeCode}>
                <td>{lossType.lossTypeCode}</td>
                <td>{valueOrDash(lossType.description)}</td>
                <td>{valueOrDash(lossType.dateUpdated?.slice(0, 10))}</td>
                <td className="actions-column">
                  <div className="table-actions">
                    <Link
                      aria-label={`Edit ${lossType.description || `loss type ${lossType.lossTypeCode}`}`}
                      className="button button-secondary button-small"
                      href={editPath(lossType.lossTypeCode)}
                    >
                      Edit
                    </Link>
                    <Link
                      aria-label={`Delete ${lossType.description || `loss type ${lossType.lossTypeCode}`}`}
                      className="button button-secondary button-small"
                      href={deleteCheckPath(lossType.lossTypeCode)}
                    >
                      Delete
                    </Link>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default async function LossTypeListPage({
  searchParams,
  routePath = "/validation-data/loss-types",
}: LossTypeListPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard
          message="Loss description maintenance is temporarily unavailable."
          routePath={routePath}
        />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard
          message="You do not have permission to maintain loss descriptions."
          routePath="/home"
        />
      </main>
    );

  const query = await searchParams;
  const saved = getQueryValue(query.saved);
  const error = getQueryValue(query.error);
  try {
    const lossTypes = await getLossTypes();
    const notice =
      saved === "created"
        ? "Loss description added successfully."
        : saved === "updated"
          ? "Loss description updated successfully."
          : saved === "deleted"
            ? "Loss description deleted successfully."
            : error;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="loss-type-list-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Operational</p>
              <h1 id="loss-type-list-title">Loss Description Maintenance</h1>
              <p>Maintain the loss descriptions used by loss/theft workflows.</p>
            </div>
            <div className="button-row">
              <Link className="button button-secondary" href="/validation-data">
                Validation Data
              </Link>
              <Link className="button button-secondary" href="/home">
                Home
              </Link>
            </div>
          </header>
          {notice ? (
            <div
              className={`notice ${error ? "notice-error" : "notice-success"}`}
              role={error ? "alert" : "status"}
            >
              {notice}
            </div>
          ) : null}
          <LossTypeTable lossTypes={lossTypes} />
          <LossTypeForm
            action={createLossTypeAction}
            lossType={{
              lossTypeCode: 0,
              description: "",
              dateCreated: null,
              dateUpdated: null,
              createdByUserCode: null,
              modifiedByUserCode: null,
              isDeleted: false,
            }}
            mode="create"
          />
          <div className="vehicle-footer-actions">
            <form action={logoutAction}>
              <button className="button button-secondary" type="submit">
                Sign out
              </button>
            </form>
          </div>
        </section>
      </main>
    );
  } catch (caughtError) {
    if (caughtError instanceof LossTypeApiError && caughtError.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS loss type list request failed",
      caughtError instanceof Error ? caughtError.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Loss descriptions could not be loaded." routePath={routePath} />
      </main>
    );
  }
}
