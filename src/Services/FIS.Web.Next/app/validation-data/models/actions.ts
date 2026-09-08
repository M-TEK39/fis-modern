"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import {
  createModel,
  deleteModel,
  getModelDeleteCheck,
  ModelApiError,
  updateModel,
  type ModelWriteInput,
} from "@/lib/api-models";
import { getSession } from "@/lib/session";

export type ModelActionState = {
  status: "idle" | "error";
  message?: string;
};

const initialState: ModelActionState = { status: "idle" };

class ModelValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getRequiredCode(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0 || parsed > 32767) {
    throw new ModelValidationError(`${label} is required.`);
  }
  return parsed;
}

function getOptionalCode(formData: FormData, key: string) {
  const value = getText(formData, key);
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0 || parsed > 32767) {
    throw new ModelValidationError("A selected reference value is invalid.");
  }
  return parsed;
}

function getRequiredInteger(
  formData: FormData,
  key: string,
  label: string,
  maxLength: number,
  minLength = 1,
  max = 2_147_483_647,
) {
  const value = getText(formData, key);
  if (!value) throw new ModelValidationError(`${label} is required.`);
  if (!/^\d+$/.test(value) || value.length < minLength || value.length > maxLength) {
    throw new ModelValidationError(
      `${label} must be a whole number between ${minLength} and ${maxLength} digits.`,
    );
  }
  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed) || parsed < 0 || parsed > max) {
    throw new ModelValidationError(`${label} is outside the supported range.`);
  }
  return parsed;
}

function getOptionalInteger(formData: FormData, key: string, label: string, max = 2_147_483_647) {
  const value = getText(formData, key);
  if (!value) return null;
  if (!/^\d+$/.test(value)) throw new ModelValidationError(`${label} must be a whole number.`);
  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed) || parsed < 0 || parsed > max) {
    throw new ModelValidationError(`${label} is outside the supported range.`);
  }
  return parsed;
}

function getRequiredDecimal(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  const parsed = Number(value);
  if (!value || !Number.isFinite(parsed) || parsed < 0) {
    throw new ModelValidationError(`${label} is required and must be a number.`);
  }
  return parsed;
}

function getOptionalDecimal(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0) {
    throw new ModelValidationError(`${label} must be a number.`);
  }
  return parsed;
}

function getModelDescription(formData: FormData) {
  const value = getText(formData, "modelDescription");
  if (!value) throw new ModelValidationError("Model description is required.");
  if (value.length > 60)
    throw new ModelValidationError("Model description must be 60 characters or fewer.");
  return value;
}

function getModelInput(formData: FormData): ModelWriteInput {
  const engineType = getText(formData, "engineType");
  if (engineType.length > 30)
    throw new ModelValidationError("Engine type must be 30 characters or fewer.");

  const vemmCode = getText(formData, "vemmCode");
  if (vemmCode.length < 7 || vemmCode.length > 20) {
    throw new ModelValidationError("VEMM code must contain between 7 and 20 characters.");
  }

  const transmission = getText(formData, "transmission");
  if (transmission !== "M" && transmission !== "A") {
    throw new ModelValidationError("Transmission must be Manual or Automatic.");
  }

  return {
    makeCode: getRequiredCode(formData, "makeCode", "Make"),
    unitOfMeasureCode: getRequiredCode(formData, "unitOfMeasureCode", "Unit of measure"),
    fuelTypeCode: getRequiredCode(formData, "fuelTypeCode", "Fuel type"),
    licenceCode: getRequiredCode(formData, "licenceCode", "Driver licence"),
    maintenanceTriggerCode: getOptionalCode(formData, "maintenanceTriggerCode"),
    classCode: getRequiredCode(formData, "classCode", "Class"),
    typeCode: getOptionalCode(formData, "typeCode"),
    modelDescription: getModelDescription(formData),
    engineType: engineType || null,
    engineCapacity: getRequiredInteger(formData, "engineCapacity", "Engine capacity", 5, 1, 32767),
    ratedPower: getRequiredInteger(formData, "ratedPower", "Rated power", 5, 1, 32767),
    fuelTankCapacity: getRequiredInteger(
      formData,
      "fuelTankCapacity",
      "Fuel tank capacity",
      4,
      1,
      32767,
    ),
    targetConsumption: getRequiredDecimal(formData, "targetConsumption", "Target consumption"),
    targetTyreLife: getRequiredInteger(formData, "targetTyreLife", "Target tyre life", 5, 3),
    serviceInterval: getRequiredInteger(formData, "serviceInterval", "Service interval", 6),
    vemmCode,
    licenceFeeCode: getRequiredCode(formData, "licenceFeeCode", "Licence fee"),
    gvm: getRequiredInteger(formData, "gvm", "GVM", 6, 2),
    transmission,
    wesbankKilosPerLitre: getOptionalDecimal(
      formData,
      "wesbankKilosPerLitre",
      "Wesbank kilos per litre",
    ),
  };
}

function getModelCode(formData: FormData) {
  return getRequiredCode(formData, "modelCode", "Model code");
}

async function authorizeModelMaintenance() {
  const session = await getSession();
  if (session.status === "unavailable") {
    return {
      ok: false as const,
      message: "The sign-in service is temporarily unavailable. Please try again.",
    };
  }
  if (session.status !== "authenticated") {
    return {
      ok: false as const,
      message: "Your session has expired. Sign in again before continuing.",
    };
  }
  if (!hasVehicleManagementPermission(session.accessLevel)) {
    return {
      ok: false as const,
      message: "You do not have permission to maintain vehicle models.",
    };
  }
  return { ok: true as const };
}

function apiErrorMessage(error: unknown, operation: string) {
  if (error instanceof ModelApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return `The vehicle model ${operation} service is temporarily unavailable. Please try again.`;
    return error.message;
  }
  return `The vehicle model could not be ${operation}. Please try again.`;
}

function revalidateModelRoutes() {
  revalidatePath("/validation-data");
  revalidatePath("/validation-data/models");
  revalidatePath("/Validation/MNT_model.aspx");
}

export async function createModelAction(
  _previousState: ModelActionState = initialState,
  formData: FormData,
): Promise<ModelActionState> {
  const access = await authorizeModelMaintenance();
  if (!access.ok) return { status: "error", message: access.message };

  try {
    const created = await createModel(getModelInput(formData));
    if (!created)
      throw new ModelApiError("invalid-response", "The FIS API did not return the created model.");
  } catch (error) {
    return {
      status: "error",
      message:
        error instanceof ModelValidationError ? error.message : apiErrorMessage(error, "created"),
    };
  }

  revalidateModelRoutes();
  redirect("/validation-data/models?saved=created");
}

export async function updateModelAction(
  _previousState: ModelActionState = initialState,
  formData: FormData,
): Promise<ModelActionState> {
  const access = await authorizeModelMaintenance();
  if (!access.ok) return { status: "error", message: access.message };

  let modelCode = 0;
  try {
    modelCode = getModelCode(formData);
    const updated = await updateModel(modelCode, getModelInput(formData));
    if (!updated)
      throw new ModelApiError("invalid-response", "The FIS API did not return the updated model.");
  } catch (error) {
    return {
      status: "error",
      message:
        error instanceof ModelValidationError ? error.message : apiErrorMessage(error, "updated"),
    };
  }

  revalidateModelRoutes();
  redirect(
    `/validation-data/models?saved=updated&modelCode=${encodeURIComponent(String(modelCode))}`,
  );
}

export async function deleteModelAction(formData: FormData) {
  const access = await authorizeModelMaintenance();
  let modelCode = 0;
  try {
    modelCode = getModelCode(formData);
  } catch (error) {
    redirect(
      `/validation-data/models?error=${encodeURIComponent(error instanceof Error ? error.message : "Model code is invalid.")}`,
    );
  }
  if (!access.ok) {
    redirect(
      `/Validation/MNT_Model_Del_Check.aspx?code=${encodeURIComponent(String(modelCode))}&error=${encodeURIComponent(access.message)}`,
    );
  }

  let dependencies;
  try {
    dependencies = await getModelDeleteCheck(modelCode);
  } catch (error) {
    redirect(
      `/Validation/MNT_Model_Del_Check.aspx?code=${modelCode}&error=${encodeURIComponent(apiErrorMessage(error, "deleted"))}`,
    );
  }
  if (!dependencies.canDelete) {
    redirect(
      `/Validation/MNT_Model_Del_Check.aspx?code=${modelCode}&error=${encodeURIComponent("This model cannot be deleted while vehicles are linked to it.")}`,
    );
  }

  try {
    await deleteModel(modelCode);
  } catch (error) {
    redirect(
      `/Validation/MNT_Model_Del_Check.aspx?code=${modelCode}&error=${encodeURIComponent(apiErrorMessage(error, "deleted"))}`,
    );
  }

  revalidateModelRoutes();
  redirect("/validation-data/models?saved=deleted");
}
