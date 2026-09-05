import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { updateModelAction } from "@/app/validation-data/models/actions";
import ModelForm from "@/app/validation-data/models/model-form";
import { getModel, getModelReferenceData, ModelApiError } from "@/lib/api-models";
import { getSession } from "@/lib/session";

type ModelEditPageProps = { searchParams: Promise<Record<string, string | string[] | undefined>> };

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function parseCode(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 && parsed <= 32767 ? parsed : null;
}

function ErrorCard({ message }: Readonly<{ message: string }>) {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">Model maintenance</p><h2>{message}</h2><Link className="button button-secondary" href="/Validation/MNT_model.aspx">Model Maintenance</Link></section>;
}

export default async function ModelEditPage({ searchParams }: ModelEditPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/Validation/MNT_Model_Edit.aspx" /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><ErrorCard message="Model maintenance is temporarily unavailable." /></main>;
  if (!hasVehicleManagementPermission(session.accessLevel)) return <main className="page-shell vehicle-page-shell"><ErrorCard message="You do not have permission to edit vehicle models." /></main>;

  const query = await searchParams;
  const modelCode = parseCode(getQueryValue(query.cmbmodel) ?? getQueryValue(query.modelCode) ?? getQueryValue(query.code));
  if (!modelCode) return <main className="page-shell vehicle-page-shell"><ErrorCard message="Select a model before opening edit." /></main>;

  try {
    const [model, referenceData] = await Promise.all([getModel(modelCode), getModelReferenceData()]);
    if (!model) return <main className="page-shell vehicle-page-shell"><ErrorCard message={`Model ${modelCode} was not found.`} /></main>;
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="model-edit-title"><header className="vehicle-page-header"><div><p className="eyebrow">Validation / Vehicle</p><h1 id="model-edit-title">Edit Model</h1><p>Update model {modelCode} without dropping any legacy specification fields.</p></div><Link className="button button-secondary" href="/Validation/MNT_model.aspx">Model Maintenance</Link></header><ModelForm action={updateModelAction} model={model} mode="update" referenceData={referenceData} /></section></main>;
  } catch (error) {
    if (error instanceof ModelApiError && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={`/Validation/MNT_Model_Edit.aspx?cmbmodel=${modelCode}`} /></main>;
    if (error instanceof ModelApiError && error.status === 404) return <main className="page-shell vehicle-page-shell"><ErrorCard message={`Model ${modelCode} was not found.`} /></main>;
    console.error("FIS model edit request failed", error instanceof Error ? error.message : "unknown error");
    return <main className="page-shell vehicle-page-shell"><ErrorCard message="Model details could not be loaded." /></main>;
  }
}
