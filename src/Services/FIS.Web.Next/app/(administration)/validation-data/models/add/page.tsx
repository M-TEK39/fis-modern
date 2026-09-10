import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { createModelAction } from "@/app/(administration)/validation-data/models/actions";
import ModelForm from "@/app/(administration)/validation-data/models/model-form";
import {
  getModelReferenceData,
  ModelApiError,
  type ModelRecord,
} from "@/lib/api/reference-data/api-models";
import { getSession } from "@/lib/auth/session";

const emptyModel: ModelRecord = {
  modelCode: 0,
  modelDescription: "",
  makeCode: 0,
  makeDescription: null,
  unitOfMeasureCode: 0,
  fuelTypeCode: 0,
  licenceCode: 0,
  maintenanceTriggerCode: null,
  classCode: 0,
  typeCode: null,
  engineType: null,
  engineCapacity: 0,
  ratedPower: 0,
  fuelTankCapacity: 0,
  targetConsumption: 0,
  targetTyreLife: 0,
  serviceInterval: 0,
  vemmCode: "???",
  licenceFeeCode: 0,
  gvm: 0,
  transmission: "M",
  wesbankKilosPerLitre: null,
  dateCreated: null,
  dateUpdated: null,
  createdByUserCode: null,
  modifiedByUserCode: null,
  isDeleted: false,
};

function ErrorCard({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Model maintenance</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_model.aspx">
        Model Maintenance
      </Link>
    </section>
  );
}

export default async function ModelAddPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Model_Add.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Model maintenance is temporarily unavailable." />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to add vehicle models." />
      </main>
    );

  try {
    const referenceData = await getModelReferenceData();
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="model-add-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Vehicle</p>
              <h1 id="model-add-title">Add New Model</h1>
              <p>Add a complete vehicle model record to the legacy model table.</p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_model.aspx">
              Model Maintenance
            </Link>
          </header>
          <ModelForm
            action={createModelAction}
            model={emptyModel}
            mode="create"
            referenceData={referenceData}
          />
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof ModelApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/Validation/MNT_Model_Add.aspx" />
        </main>
      );
    console.error(
      "FIS model add reference data request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="The model reference data could not be loaded." />
      </main>
    );
  }
}
