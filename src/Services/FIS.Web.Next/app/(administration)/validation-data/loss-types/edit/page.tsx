import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { updateLossTypeAction } from "@/app/(administration)/validation-data/loss-types/actions";
import LossTypeForm from "@/app/(administration)/validation-data/loss-types/loss-type-form";
import { getLossType, LossTypeApiError } from "@/lib/api/fleet-operations/api-loss-types";
import { getSession } from "@/lib/auth/session";
import RouteLoading from "@/components/app-shell/route-loading";

type LossTypeEditPageProps = {
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
      <p className="eyebrow">Loss description maintenance</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_Loss_Type.aspx">
        Loss Description Maintenance
      </Link>
    </section>
  );
}

async function LossTypeEditPageContent({ searchParams }: LossTypeEditPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Loss_Type_Edit.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Loss description maintenance is temporarily unavailable." />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to edit loss descriptions." />
      </main>
    );

  const query = await searchParams;
  const lossTypeCode = parseCode(
    getQueryValue(query.cmbLoss) ??
      getQueryValue(query.cmbloss) ??
      getQueryValue(query.lossTypeCode) ??
      getQueryValue(query.code),
  );
  if (!lossTypeCode)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Select a loss description before opening edit." />
      </main>
    );

  try {
    const lossType = await getLossType(lossTypeCode);
    if (!lossType)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Loss type ${lossTypeCode} was not found.`} />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="loss-type-edit-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Operational</p>
              <h1 id="loss-type-edit-title">Edit Loss Description</h1>
              <p>Update loss type {lossTypeCode} without changing its legacy code.</p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_Loss_Type.aspx">
              Loss Description Maintenance
            </Link>
          </header>
          <LossTypeForm action={updateLossTypeAction} lossType={lossType} mode="update" />
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof LossTypeApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery
            returnPath={`/Validation/MNT_Loss_Type_Edit.aspx?cmbLoss=${lossTypeCode}`}
          />
        </main>
      );
    if (error instanceof LossTypeApiError && error.status === 404)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Loss type ${lossTypeCode} was not found.`} />
        </main>
      );
    console.error(
      "FIS loss type edit request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Loss description details could not be loaded." />
      </main>
    );
  }
}

export default function LossTypeEditPage(
  props: NonNullable<Parameters<typeof LossTypeEditPageContent>[0]>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LossTypeEditPageContent {...props} />
    </Suspense>
  );
}
