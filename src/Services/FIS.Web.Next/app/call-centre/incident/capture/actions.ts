"use server";

import { redirect } from "next/navigation";

import {
  CallCentreApiError,
  createAccidentIncident,
  createAccidentTowing,
  createCallCentreIncident,
  createHiJackIncident,
  createLossIncident,
  createLossTowing,
  createRoadAssistanceIncident,
} from "@/lib/api-call-centre";
import { getSession } from "@/lib/session";

const CAPTURE_PATH = "/call-centre/incident/capture";
const ROAD_CAPTURE_PATH = "/CallCentre/MNT_road_getdata.aspx";
const ACCIDENT_CAPTURE_PATH = "/CallCentre/MNT_accident_getdata.aspx";
const ACCIDENT_SPLIT_PATH = "/CallCentre/MNT_accident_split.aspx";
const ACCIDENT_TOW_DETAIL_PATH = "/CallCentre/MNT_accident_towdetail.aspx";
const ACCIDENT_SHOW_DETAIL_PATH = "/CallCentre/MNT_accident_showdetail.aspx";
const HIJACK_CAPTURE_PATH = "/CallCentre/MNT_highjack_getdata.aspx";
const HIJACK_SHOW_DETAIL_PATH = "/CallCentre/MNT_highjack_showdetail.aspx";
const LOSS_CAPTURE_PATH = "/CallCentre/MNT_loss_getdata.aspx";
const LOSS_SPLIT_PATH = "/CallCentre/MNT_loss_split.aspx";
const LOSS_TOW_DETAIL_PATH = "/CallCentre/MNT_loss_towdetail.aspx";
const LOSS_SHOW_DETAIL_PATH = "/CallCentre/MNT_loss_showdetail.aspx";
const CALL_CENTRE_ROLE = "Call Centre";

function getText(formData: FormData, ...keys: string[]) {
  for (const key of keys) {
    const value = formData.get(key);
    if (typeof value === "string") {
      return value.trim();
    }
  }

  return "";
}

function redirectWithError(message: string, vmfCode = "", incidentType = "", path = CAPTURE_PATH): never {
  const params = new URLSearchParams({ error: message });
  if (vmfCode) {
    params.set("vmfCode", vmfCode);
  }
  if (incidentType) {
    params.set("incidentType", incidentType);
  }

  redirect(`${path}?${params.toString()}`);
}

function redirectRoadWithError(message: string, vmfCode = ""): never {
  const params = new URLSearchParams({ error: message, incidentType: "Road_Assistance" });
  if (vmfCode) {
    params.set("ccVMF", vmfCode);
  }

  redirect(`${ROAD_CAPTURE_PATH}?${params.toString()}`);
}

function redirectAccidentWithError(
  message: string,
  vmfCode = "",
  path = ACCIDENT_CAPTURE_PATH,
  callCentreCode = "",
): never {
  const params = new URLSearchParams({ error: message, incidentType: "Accident" });
  if (vmfCode) {
    params.set("ccVMF", vmfCode);
  }
  if (callCentreCode) {
    params.set("cccode", callCentreCode);
  }

  redirect(`${path}?${params.toString()}`);
}

function redirectHiJackWithError(message: string, vmfCode = ""): never {
  const params = new URLSearchParams({ error: message, incidentType: "Hi-Jack" });
  if (vmfCode) {
    params.set("ccVMF", vmfCode);
  }

  redirect(`${HIJACK_CAPTURE_PATH}?${params.toString()}`);
}

function redirectLossWithError(
  message: string,
  vmfCode = "",
  path = LOSS_CAPTURE_PATH,
  callCentreCode = "",
): never {
  const params = new URLSearchParams({ error: message, incidentType: "Loss_Theft" });
  if (vmfCode) {
    params.set("ccVMF", vmfCode);
  }
  if (callCentreCode) {
    params.set("cccode", callCentreCode);
  }

  redirect(`${path}?${params.toString()}`);
}

async function authorizeCallCentre(vmfCode: string, incidentType = "", path = CAPTURE_PATH) {
  const session = await getSession();
  if (session.status === "unavailable") {
    redirectWithError("The sign-in service is temporarily unavailable. Please try again.", vmfCode, incidentType, path);
  }

  if (session.status !== "authenticated") {
    redirectWithError("Your session has expired. Sign in again before continuing.", vmfCode, incidentType, path);
  }

  if (!session.roles.some((role) => role.localeCompare(CALL_CENTRE_ROLE, undefined, { sensitivity: "accent" }) === 0)) {
    redirectWithError("You do not have permission to capture call centre incidents.", vmfCode, incidentType, path);
  }
}

async function authorizeAccidentTowing(vmfCode: string, callCentreCode: string) {
  const session = await getSession();
  if (session.status === "unavailable") {
    redirectAccidentWithError(
      "The sign-in service is temporarily unavailable. Please try again.",
      vmfCode,
      ACCIDENT_TOW_DETAIL_PATH,
      callCentreCode,
    );
  }

  if (session.status !== "authenticated") {
    redirectAccidentWithError(
      "Your session has expired. Sign in again before continuing.",
      vmfCode,
      ACCIDENT_TOW_DETAIL_PATH,
      callCentreCode,
    );
  }

  if (!session.roles.some((role) => role.localeCompare(CALL_CENTRE_ROLE, undefined, { sensitivity: "accent" }) === 0)) {
    redirectAccidentWithError(
      "You do not have permission to capture call centre incidents.",
      vmfCode,
      ACCIDENT_TOW_DETAIL_PATH,
      callCentreCode,
    );
  }
}

async function authorizeLossTowing(vmfCode: string, callCentreCode: string) {
  const session = await getSession();
  if (session.status === "unavailable") {
    redirectLossWithError(
      "The sign-in service is temporarily unavailable. Please try again.",
      vmfCode,
      LOSS_TOW_DETAIL_PATH,
      callCentreCode,
    );
  }

  if (session.status !== "authenticated") {
    redirectLossWithError(
      "Your session has expired. Sign in again before continuing.",
      vmfCode,
      LOSS_TOW_DETAIL_PATH,
      callCentreCode,
    );
  }

  if (!session.roles.some((role) => role.localeCompare(CALL_CENTRE_ROLE, undefined, { sensitivity: "accent" }) === 0)) {
    redirectLossWithError(
      "You do not have permission to capture call centre incidents.",
      vmfCode,
      LOSS_TOW_DETAIL_PATH,
      callCentreCode,
    );
  }
}

function getPositiveInt(formData: FormData, ...keys: string[]) {
  const value = getText(formData, ...keys);
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function validateMaxLength(value: string, field: string, maxLength: number, vmfCode: string) {
  if (value.length > maxLength) {
    redirectWithError(`${field} must be ${maxLength} characters or fewer.`, vmfCode);
  }
}

function validateRoadMaxLength(value: string, field: string, maxLength: number, vmfCode: string) {
  if (value.length > maxLength) {
    redirectRoadWithError(`${field} must be ${maxLength} characters or fewer.`, vmfCode);
  }
}

function validateAccidentMaxLength(value: string, field: string, maxLength: number, vmfCode: string) {
  if (value.length > maxLength) {
    redirectAccidentWithError(`${field} must be ${maxLength} characters or fewer.`, vmfCode);
  }
}

function validateAccidentTowMaxLength(
  value: string,
  field: string,
  maxLength: number,
  vmfCode: string,
  callCentreCode: string,
) {
  if (value.length > maxLength) {
    redirectAccidentWithError(
      `${field} must be ${maxLength} characters or fewer.`,
      vmfCode,
      ACCIDENT_TOW_DETAIL_PATH,
      callCentreCode,
    );
  }
}

function validateHiJackMaxLength(value: string, field: string, maxLength: number, vmfCode: string) {
  if (value.length > maxLength) {
    redirectHiJackWithError(`${field} must be ${maxLength} characters or fewer.`, vmfCode);
  }
}

function validateLossMaxLength(value: string, field: string, maxLength: number, vmfCode: string) {
  if (value.length > maxLength) {
    redirectLossWithError(`${field} must be ${maxLength} characters or fewer.`, vmfCode);
  }
}

function validateLossTowMaxLength(
  value: string,
  field: string,
  maxLength: number,
  vmfCode: string,
  callCentreCode: string,
) {
  if (value.length > maxLength) {
    redirectLossWithError(
      `${field} must be ${maxLength} characters or fewer.`,
      vmfCode,
      LOSS_TOW_DETAIL_PATH,
      callCentreCode,
    );
  }
}

function apiErrorMessage(error: unknown) {
  if (error instanceof CallCentreApiError) {
    if (error.reason === "unauthorized") {
      return "Your session has expired. Sign in again before continuing.";
    }

    if (error.reason === "not-found") {
      return "The selected vehicle was not found.";
    }

    if (error.reason === "unavailable") {
      return "The call centre service is temporarily unavailable. Please try again.";
    }
  }

  return "The incident could not be captured. Please try again.";
}

export async function saveQueryIncidentAction(formData: FormData) {
  const vmfCodeText = getText(formData, "ccVMF", "vmfCode");
  await authorizeCallCentre(vmfCodeText);

  const vmfCode = getPositiveInt(formData, "ccVMF", "vmfCode");
  if (vmfCode === null) {
    redirectWithError("A valid vehicle must be selected before capturing an incident.");
  }

  const incidentType = getText(formData, "xinctype", "incidentType") || "Query";
  if (incidentType !== "Query") {
    redirectWithError("This form only captures Query incidents.", vmfCodeText);
  }

  const informCro = getText(formData, "xcro", "informCro") || "N";
  if (informCro !== "Y" && informCro !== "N") {
    redirectWithError("The CLO notification choice is invalid.", vmfCodeText);
  }

  const croRemarks = getText(formData, "xcrem", "croRemarks");
  validateMaxLength(getText(formData, "xtrsname", "transportOfficerName"), "Transport officer name", 60, vmfCodeText);
  validateMaxLength(getText(formData, "xtrstel", "transportOfficerTel"), "Transport officer telephone", 15, vmfCodeText);
  validateMaxLength(getText(formData, "xtrsfax", "transportOfficerFax"), "Transport officer fax", 15, vmfCodeText);
  validateMaxLength(getText(formData, "xtrseml", "transportOfficerEmail"), "Transport officer email", 30, vmfCodeText);
  validateMaxLength(getText(formData, "xcalname", "callerName"), "Caller name", 30, vmfCodeText);
  validateMaxLength(getText(formData, "xcaltel", "callerTel"), "Caller telephone", 30, vmfCodeText);
  validateMaxLength(getText(formData, "xcalfax", "callerFax"), "Caller fax", 15, vmfCodeText);
  validateMaxLength(getText(formData, "xcaleml", "callerEmail"), "Caller email", 30, vmfCodeText);
  validateMaxLength(croRemarks, "CLO remarks", 60, vmfCodeText);
  validateMaxLength(getText(formData, "xirem", "incidentRemarks"), "Incident notes", 80, vmfCodeText);
  if (informCro === "Y" && !croRemarks) {
    redirectWithError("Remarks for the CLO are required when informing the CLO.", String(vmfCode));
  }

  const callerName = getText(formData, "xcalname", "callerName");
  const transportOfficerName = getText(formData, "xtrsname", "transportOfficerName");
  const callerTel = getText(formData, "xcaltel", "callerTel");
  const transportOfficerTel = getText(formData, "xtrstel", "transportOfficerTel");
  const callerFax = getText(formData, "xcalfax", "callerFax");
  const transportOfficerFax = getText(formData, "xtrsfax", "transportOfficerFax");
  const callerEmail = getText(formData, "xcaleml", "callerEmail");
  const transportOfficerEmail = getText(formData, "xtrseml", "transportOfficerEmail");
  const callClosed = getText(formData, "xclosed", "callClosed") || "Y";
  if (callClosed !== "Y" && callClosed !== "N") {
    redirectWithError("The call closed choice is invalid.", String(vmfCode));
  }

  try {
    const code = await createCallCentreIncident({
      VmfCode: vmfCode,
      IncidentType: incidentType,
      TransportOfficerName: transportOfficerName || null,
      TransportOfficerTel: transportOfficerTel || null,
      TransportOfficerFax: transportOfficerFax || null,
      TransportOfficerEmail: transportOfficerEmail || null,
      TransportOfficerSite: getPositiveInt(formData, "xtrssite", "transportOfficerSite"),
      CallerName: callerName || transportOfficerName || null,
      CallerTel: callerName ? callerTel || null : transportOfficerTel || null,
      CallerFax: callerName ? callerFax || null : transportOfficerFax || null,
      CallerEmail: callerName ? callerEmail || null : transportOfficerEmail || null,
      InformCro: informCro === "Y" ? "Y" : "N",
      CroRemarks: croRemarks || null,
      IncidentRemarks: getText(formData, "xirem", "incidentRemarks") || null,
      NotifyListCode: getPositiveInt(formData, "xnotel", "notifyListCode"),
      CallClosed: callClosed,
    });

    const params = new URLSearchParams({ saved: "1" });
    if (code !== null) {
      params.set("code", String(code));
    }
    redirect(`${CAPTURE_PATH}?${params.toString()}`);
  } catch (error) {
    redirectWithError(apiErrorMessage(error), String(vmfCode));
  }
}

export async function saveRoadAssistanceAction(formData: FormData) {
  const vmfCodeText = getText(formData, "ccVMF", "vmfCode");
  await authorizeCallCentre(vmfCodeText, "Road_Assistance", ROAD_CAPTURE_PATH);

  const vmfCode = getPositiveInt(formData, "ccVMF", "vmfCode");
  if (vmfCode === null) {
    redirectRoadWithError("A valid vehicle must be selected before capturing an incident.", vmfCodeText);
  }

  const incidentType = getText(formData, "xinctype", "incidentType") || "Road_Assistance";
  if (incidentType !== "Road_Assistance") {
    redirectRoadWithError("This form only captures Road Assistance incidents.", vmfCodeText);
  }

  const incidentDate = getText(formData, "xincdat", "incidentDate");
  const incidentTime = getText(formData, "xinctime", "incidentTime");
  if (!/^\d{4}-\d{2}-\d{2}$/.test(incidentDate)) {
    redirectRoadWithError("Enter a valid incident date.", vmfCodeText);
  }
  if (!/^\d{2}:\d{2}$/.test(incidentTime)) {
    redirectRoadWithError("Enter the incident time in HH:mm format.", vmfCodeText);
  }

  const informCro = getText(formData, "xcro", "informCro") || "N";
  if (informCro !== "Y" && informCro !== "N") {
    redirectRoadWithError("The CLO notification choice is invalid.", vmfCodeText);
  }

  const callClosed = getText(formData, "xclosed", "callClosed") || "N";
  if (callClosed !== "Y" && callClosed !== "N") {
    redirectRoadWithError("The call closed choice is invalid.", vmfCodeText);
  }

  const transportOfficerName = getText(formData, "xtrsname", "transportOfficerName");
  const transportOfficerTel = getText(formData, "xtrstel", "transportOfficerTel");
  const transportOfficerFax = getText(formData, "xtrsfax", "transportOfficerFax");
  const transportOfficerEmail = getText(formData, "xtrseml", "transportOfficerEmail");
  const callerName = getText(formData, "xcalname", "callerName");
  const callerTel = getText(formData, "xcaltel", "callerTel");
  const callerFax = getText(formData, "xcalfax", "callerFax");
  const callerEmail = getText(formData, "xcaleml", "callerEmail");
  const driverName = getText(formData, "xdrvname", "driverName");
  const driverTel = getText(formData, "xdrvtel", "driverTel");
  const driverPersalno = getText(formData, "xdrvperno", "driverPersalno");
  const croRemarks = getText(formData, "xcrem", "croRemarks");
  const town = getText(formData, "x2town", "town");
  const suburb = getText(formData, "x1town", "suburb");
  const street = getText(formData, "xstreet", "street");
  const vehicleProblem = getText(formData, "xincdesc", "vehicleProblem");
  const towingRemarks = getText(formData, "xrem", "towingRemarks");
  const location = [suburb, town].filter(Boolean).join(" ; ");

  validateRoadMaxLength(transportOfficerName, "Transport officer name", 60, vmfCodeText);
  validateRoadMaxLength(transportOfficerTel, "Transport officer telephone", 15, vmfCodeText);
  validateRoadMaxLength(transportOfficerFax, "Transport officer fax", 15, vmfCodeText);
  validateRoadMaxLength(transportOfficerEmail, "Transport officer email", 30, vmfCodeText);
  validateRoadMaxLength(callerName, "Caller name", 30, vmfCodeText);
  validateRoadMaxLength(callerTel, "Caller telephone", 30, vmfCodeText);
  validateRoadMaxLength(callerFax, "Caller fax", 15, vmfCodeText);
  validateRoadMaxLength(callerEmail, "Caller email", 30, vmfCodeText);
  validateRoadMaxLength(driverName, "Driver name", 60, vmfCodeText);
  validateRoadMaxLength(driverTel, "Driver telephone", 30, vmfCodeText);
  validateRoadMaxLength(driverPersalno, "Driver personnel number", 15, vmfCodeText);
  validateRoadMaxLength(croRemarks, "CLO remarks", 60, vmfCodeText);
  validateRoadMaxLength(town, "Town", 20, vmfCodeText);
  validateRoadMaxLength(suburb, "Suburb", 30, vmfCodeText);
  validateRoadMaxLength(street, "Street name", 30, vmfCodeText);
  validateRoadMaxLength(vehicleProblem, "Vehicle problem", 60, vmfCodeText);
  validateRoadMaxLength(towingRemarks, "Towing remarks", 50, vmfCodeText);
  validateRoadMaxLength(location, "Location", 50, vmfCodeText);
  if (informCro === "Y" && !croRemarks) {
    redirectRoadWithError("Remarks for the CLO are required when informing the CLO.", String(vmfCode));
  }

  let result: Awaited<ReturnType<typeof createRoadAssistanceIncident>>;
  try {
    result = await createRoadAssistanceIncident({
      VmfCode: vmfCode,
      IncidentType: "Road_Assistance",
      GGNumber: getText(formData, "xgg", "ggNumber") || null,
      IncidentDate: `${incidentDate}T00:00:00`,
      IncidentTime: `${incidentDate}T${incidentTime}:00`,
      IncidentTown: location || null,
      IncidentStreet: street || null,
      DriverName: driverName || transportOfficerName || null,
      DriverTel: driverName ? driverTel || null : transportOfficerTel || null,
      DriverPersalno: driverPersalno || null,
      TransportOfficerName: transportOfficerName || null,
      TransportOfficerTel: transportOfficerTel || null,
      TransportOfficerFax: transportOfficerFax || null,
      TransportOfficerEmail: transportOfficerEmail || null,
      TransportOfficerSite: getPositiveInt(formData, "xtrssite", "transportOfficerSite"),
      CallerName: callerName || transportOfficerName || null,
      CallerTel: callerName ? callerTel || null : transportOfficerTel || null,
      CallerFax: callerName ? callerFax || null : transportOfficerFax || null,
      CallerEmail: callerName ? callerEmail || null : transportOfficerEmail || null,
      InformCro: informCro,
      CroRemarks: croRemarks || null,
      IncidentRemarks: null,
      NotifyListCode: getPositiveInt(formData, "xnotc", "notifyListCode"),
      CallClosed: callClosed,
      TowingLocationStart: location || null,
      VehicleProblem: vehicleProblem || null,
      TowingRemarks: towingRemarks || null,
      TowTruckCode: getPositiveInt(formData, "xtruckcod", "towTruckCode"),
    });

  } catch (error) {
    redirectRoadWithError(apiErrorMessage(error), String(vmfCode));
  }

  const params = new URLSearchParams({
    saved: "1",
    incidentType: "Road_Assistance",
    ccVMF: String(vmfCode),
    code: String(result.callCentreCode),
  });
  redirect(`${ROAD_CAPTURE_PATH}?${params.toString()}`);
}

export async function saveAccidentAction(formData: FormData) {
  const vmfCodeText = getText(formData, "ccVMF", "vmfCode");
  await authorizeCallCentre(vmfCodeText, "Accident", ACCIDENT_CAPTURE_PATH);

  const vmfCode = getPositiveInt(formData, "ccVMF", "vmfCode");
  if (vmfCode === null) {
    redirectAccidentWithError("A valid vehicle must be selected before capturing an incident.", vmfCodeText);
  }

  const incidentType = getText(formData, "xinctype", "incidentType") || "Accident";
  if (incidentType !== "Accident") {
    redirectAccidentWithError("This form only captures Accident incidents.", vmfCodeText);
  }

  const incidentDate = getText(formData, "xincdat", "incidentDate");
  const incidentTime = getText(formData, "xinctime", "incidentTime");
  if (!/^\d{4}-\d{2}-\d{2}$/.test(incidentDate)) {
    redirectAccidentWithError("Enter a valid accident date.", vmfCodeText);
  }
  if (incidentTime && !/^\d{2}:\d{2}$/.test(incidentTime)) {
    redirectAccidentWithError("Enter the accident time in HH:mm format.", vmfCodeText);
  }

  const informCro = getText(formData, "xcro", "informCro") || "N";
  if (informCro !== "Y" && informCro !== "N") {
    redirectAccidentWithError("The CLO notification choice is invalid.", vmfCodeText);
  }

  const callClosed = getText(formData, "xclosed", "callClosed") || "N";
  if (callClosed !== "Y" && callClosed !== "N") {
    redirectAccidentWithError("The call closed choice is invalid.", vmfCodeText);
  }

  const death = getText(formData, "txtDeath", "death") || "?";
  const injured = getText(formData, "txtInjured", "injured") || "?";
  if (!["?", "Y", "N"].includes(death) || !["?", "Y", "N"].includes(injured)) {
    redirectAccidentWithError("The death and injury choices are invalid.", vmfCodeText);
  }

  const towNeed = getText(formData, "xtowneed", "towNeed");
  if (towNeed !== "Y" && towNeed !== "N") {
    redirectAccidentWithError("Choose whether a tow truck is needed.", vmfCodeText);
  }

  const transportOfficerName = getText(formData, "xtrsname", "transportOfficerName");
  const transportOfficerTel = getText(formData, "xtrstel", "transportOfficerTel");
  const transportOfficerFax = getText(formData, "xtrsfax", "transportOfficerFax");
  const transportOfficerEmail = getText(formData, "xtrseml", "transportOfficerEmail");
  const callerName = getText(formData, "xcalname", "callerName");
  const callerTel = getText(formData, "xcaltel", "callerTel");
  const callerFax = getText(formData, "xcalfax", "callerFax");
  const callerEmail = getText(formData, "xcaleml", "callerEmail");
  const driverName = getText(formData, "xdrvname", "driverName");
  const driverTel = getText(formData, "xdrvtel", "driverTel");
  const driverPersalno = getText(formData, "xdrvperno", "driverPersalno");
  const croRemarks = getText(formData, "xcrem", "croRemarks");
  const accidentDescription = getText(formData, "xincdesc", "accidentDescription");
  const damageDescription = getText(formData, "txtDamage", "damageDescription");
  const thirdPartyRegistration = getText(formData, "txtThregno", "thirdPartyRegistration");
  const thirdPartyOwner = getText(formData, "txtThname", "thirdPartyOwner");
  const thirdPartyTelephone = getText(formData, "txtThtel", "thirdPartyTelephone");
  const suburb = getText(formData, "x1town", "suburb");
  const town = getText(formData, "x2town", "town");
  const street = getText(formData, "xstreet", "street");
  const accidentNotes = getText(formData, "txtNotes", "accidentNotes");
  const occurencePlace = `${suburb} ; ${town}`;

  validateAccidentMaxLength(transportOfficerName, "Transport officer name", 60, vmfCodeText);
  validateAccidentMaxLength(transportOfficerTel, "Transport officer telephone", 15, vmfCodeText);
  validateAccidentMaxLength(transportOfficerFax, "Transport officer fax", 15, vmfCodeText);
  validateAccidentMaxLength(transportOfficerEmail, "Transport officer email", 30, vmfCodeText);
  validateAccidentMaxLength(callerName, "Caller name", 40, vmfCodeText);
  validateAccidentMaxLength(callerTel, "Caller telephone", 30, vmfCodeText);
  validateAccidentMaxLength(callerFax, "Caller fax", 15, vmfCodeText);
  validateAccidentMaxLength(callerEmail, "Caller email", 30, vmfCodeText);
  validateAccidentMaxLength(driverName, "Driver name", 60, vmfCodeText);
  validateAccidentMaxLength(driverTel, "Driver telephone", 30, vmfCodeText);
  validateAccidentMaxLength(driverPersalno, "Driver personnel number", 15, vmfCodeText);
  validateAccidentMaxLength(croRemarks, "CLO remarks", 60, vmfCodeText);
  validateAccidentMaxLength(accidentDescription, "Accident description", 60, vmfCodeText);
  validateAccidentMaxLength(damageDescription, "Damage description", 60, vmfCodeText);
  validateAccidentMaxLength(thirdPartyRegistration, "Private party registration", 8, vmfCodeText);
  validateAccidentMaxLength(thirdPartyOwner, "Private party name", 30, vmfCodeText);
  validateAccidentMaxLength(thirdPartyTelephone, "Private party telephone", 30, vmfCodeText);
  validateAccidentMaxLength(suburb, "Suburb", 50, vmfCodeText);
  validateAccidentMaxLength(town, "Town", 50, vmfCodeText);
  validateAccidentMaxLength(street, "Street name", 30, vmfCodeText);
  validateAccidentMaxLength(accidentNotes, "Accident notes", 55, vmfCodeText);
  if (informCro === "Y" && !croRemarks) {
    redirectAccidentWithError("Remarks for the CLO are required when informing the CLO.", String(vmfCode));
  }

  let result: Awaited<ReturnType<typeof createAccidentIncident>>;
  try {
    result = await createAccidentIncident({
      VmfCode: vmfCode,
      IncidentType: "Accident",
      TransportOfficerName: transportOfficerName || null,
      TransportOfficerTel: transportOfficerTel || null,
      TransportOfficerFax: transportOfficerFax || null,
      TransportOfficerEmail: transportOfficerEmail || null,
      TransportOfficerSite: getPositiveInt(formData, "xtrssite", "transportOfficerSite"),
      CallerName: callerName || transportOfficerName || null,
      CallerTel: callerName ? callerTel || null : transportOfficerTel || null,
      CallerFax: callerName ? callerFax || null : transportOfficerFax || null,
      CallerEmail: callerName ? callerEmail || null : transportOfficerEmail || null,
      InformCro: informCro,
      CroRemarks: croRemarks || null,
      IncidentRemarks: null,
      NotifyListCode: getPositiveInt(formData, "xnotc", "notifyListCode"),
      CallClosed: callClosed,
      IncidentDate: `${incidentDate}T00:00:00`,
      IncidentTime: incidentTime ? `${incidentDate}T${incidentTime}:00` : null,
      AccidentDescription: accidentDescription || null,
      DamageDescription: damageDescription || null,
      ThirdPartyRegistration: thirdPartyRegistration || null,
      ThirdPartyOwner: thirdPartyOwner || null,
      ThirdPartyTelephone: thirdPartyTelephone || null,
      Death: death,
      Injured: injured,
      OccurencePlace: occurencePlace,
      TowNeed: towNeed,
      AccidentNotes: accidentNotes || null,
      DriverName: driverName || transportOfficerName || null,
      DriverTel: driverName ? driverTel || null : transportOfficerTel || null,
      DriverPersalno: driverPersalno || null,
      AccidentDriverName: driverName || null,
      AccidentDriverTel: driverName ? driverTel || null : null,
      AccidentDriverEmployNumber: driverPersalno || null,
    });
  } catch (error) {
    redirectAccidentWithError(apiErrorMessage(error), String(vmfCode));
  }

  const params = new URLSearchParams({
    xtowneed: towNeed,
    ccVMF: String(vmfCode),
    cccode: String(result.callCentreCode),
    xinctype: "Accident",
    xgg: getText(formData, "xgg", "ggNumber"),
    xgp: getText(formData, "xgp", "registrationNumber"),
    txtDamage: damageDescription,
    accidentCode: String(result.accidentCode),
  });
  redirect(`${ACCIDENT_SPLIT_PATH}?${params.toString()}`);
}

export async function saveHiJackAction(formData: FormData) {
  const vmfCodeText = getText(formData, "ccVMF", "vmfCode");
  await authorizeCallCentre(vmfCodeText, "Hi-Jack", HIJACK_CAPTURE_PATH);

  const vmfCode = getPositiveInt(formData, "ccVMF", "vmfCode");
  if (vmfCode === null) {
    redirectHiJackWithError("A valid vehicle must be selected before capturing an incident.", vmfCodeText);
  }

  const incidentType = getText(formData, "xinctype", "incidentType") || "Hi-Jack";
  if (incidentType !== "Hi-Jack") {
    redirectHiJackWithError("This form only captures Hi-Jack incidents.", vmfCodeText);
  }

  const incidentDate = getText(formData, "xincdat", "incidentDate");
  const incidentTime = getText(formData, "xinctime", "incidentTime");
  if (!/^\d{4}-\d{2}-\d{2}$/.test(incidentDate)) {
    redirectHiJackWithError("Enter a valid Hi-Jack date.", vmfCodeText);
  }
  if (incidentTime && !/^\d{2}:\d{2}$/.test(incidentTime)) {
    redirectHiJackWithError("Enter the Hi-Jack time in HH:mm format.", vmfCodeText);
  }

  const informCro = getText(formData, "xcro", "informCro") || "N";
  if (informCro !== "Y" && informCro !== "N") {
    redirectHiJackWithError("The CLO notification choice is invalid.", vmfCodeText);
  }

  const callClosed = getText(formData, "xclosed", "callClosed") || "N";
  if (callClosed !== "Y" && callClosed !== "N") {
    redirectHiJackWithError("The call closed choice is invalid.", vmfCodeText);
  }

  const transportOfficerName = getText(formData, "xtrsname", "transportOfficerName");
  const transportOfficerTel = getText(formData, "xtrstel", "transportOfficerTel");
  const transportOfficerFax = getText(formData, "xtrsfax", "transportOfficerFax");
  const transportOfficerEmail = getText(formData, "xtrseml", "transportOfficerEmail");
  const callerName = getText(formData, "xcalname", "callerName");
  const callerTel = getText(formData, "xcaltel", "callerTel");
  const callerFax = getText(formData, "xcalfax", "callerFax");
  const callerEmail = getText(formData, "xcaleml", "callerEmail");
  const driverName = getText(formData, "xdrvname", "driverName");
  const driverTel = getText(formData, "xdrvtel", "driverTel");
  const driverPersalno = getText(formData, "xdrvperno", "driverPersalno");
  const croRemarks = getText(formData, "xcrem", "croRemarks");
  const suburb = getText(formData, "x1town", "suburb");
  const town = getText(formData, "x2town", "town");
  const street = getText(formData, "xstreet", "street");
  const incidentDescription = getText(formData, "xincdesc", "incidentDescription");
  const incidentRemarks = getText(formData, "xrem", "incidentRemarks");
  const incidentTown = [suburb, town].filter(Boolean).join(" ; ");

  validateHiJackMaxLength(transportOfficerName, "Transport officer name", 30, vmfCodeText);
  validateHiJackMaxLength(transportOfficerTel, "Transport officer telephone", 15, vmfCodeText);
  validateHiJackMaxLength(transportOfficerFax, "Transport officer fax", 15, vmfCodeText);
  validateHiJackMaxLength(transportOfficerEmail, "Transport officer email", 30, vmfCodeText);
  validateHiJackMaxLength(callerName, "Caller name", 30, vmfCodeText);
  validateHiJackMaxLength(callerTel, "Caller telephone", 30, vmfCodeText);
  validateHiJackMaxLength(callerFax, "Caller fax", 15, vmfCodeText);
  validateHiJackMaxLength(callerEmail, "Caller email", 30, vmfCodeText);
  validateHiJackMaxLength(driverName, "Driver name", 60, vmfCodeText);
  validateHiJackMaxLength(driverTel, "Driver telephone", 30, vmfCodeText);
  validateHiJackMaxLength(driverPersalno, "Driver personnel number", 15, vmfCodeText);
  validateHiJackMaxLength(croRemarks, "CLO remarks", 60, vmfCodeText);
  validateHiJackMaxLength(suburb, "Suburb", 30, vmfCodeText);
  validateHiJackMaxLength(town, "Town", 20, vmfCodeText);
  validateHiJackMaxLength(incidentTown, "Incident location", 50, vmfCodeText);
  validateHiJackMaxLength(street, "Street name", 30, vmfCodeText);
  validateHiJackMaxLength(incidentDescription, "Hi-Jack description", 60, vmfCodeText);
  validateHiJackMaxLength(incidentRemarks, "Remarks", 80, vmfCodeText);
  if (informCro === "Y" && !croRemarks) {
    redirectHiJackWithError("Remarks for the CLO are required when informing the CLO.", String(vmfCode));
  }

  try {
    const result = await createHiJackIncident({
      VmfCode: vmfCode,
      IncidentType: "Hi-Jack",
      TransportOfficerName: transportOfficerName || null,
      TransportOfficerTel: transportOfficerTel || null,
      TransportOfficerFax: transportOfficerFax || null,
      TransportOfficerEmail: transportOfficerEmail || null,
      TransportOfficerSite: getPositiveInt(formData, "xtrssite", "transportOfficerSite"),
      CallerName: callerName || transportOfficerName || null,
      CallerTel: callerName ? callerTel || null : transportOfficerTel || null,
      CallerFax: callerName ? callerFax || null : transportOfficerFax || null,
      CallerEmail: callerName ? callerEmail || null : transportOfficerEmail || null,
      InformCro: informCro,
      CroRemarks: croRemarks || null,
      IncidentRemarks: incidentRemarks || null,
      NotifyListCode: getPositiveInt(formData, "xnotc", "notifyListCode"),
      CallClosed: callClosed,
      IncidentDate: `${incidentDate}T00:00:00`,
      IncidentTime: incidentTime ? `${incidentDate}T${incidentTime}:00` : null,
      IncidentTown: incidentTown || null,
      IncidentStreet: street || null,
      DriverName: driverName || transportOfficerName || null,
      DriverTel: driverName ? driverTel || null : transportOfficerTel || null,
      DriverPersalno: driverPersalno || null,
      IncidentDesc: incidentDescription || null,
    });

    const params = new URLSearchParams({
      saved: "1",
      incidentType: "Hi-Jack",
      ccVMF: String(vmfCode),
      code: String(result.callCentreCode),
    });
    redirect(`${HIJACK_SHOW_DETAIL_PATH}?${params.toString()}`);
  } catch (error) {
    redirectHiJackWithError(apiErrorMessage(error), String(vmfCode));
  }
}

export async function saveLossAction(formData: FormData) {
  const vmfCodeText = getText(formData, "ccVMF", "vmfCode");
  await authorizeCallCentre(vmfCodeText, "Loss_Theft", LOSS_CAPTURE_PATH);

  const vmfCode = getPositiveInt(formData, "ccVMF", "vmfCode");
  if (vmfCode === null) {
    redirectLossWithError("A valid vehicle must be selected before capturing an incident.", vmfCodeText);
  }

  const incidentType = getText(formData, "xinctype", "incidentType") || "Loss_Theft";
  if (incidentType !== "Loss_Theft") {
    redirectLossWithError("This form only captures Loss/Theft incidents.", vmfCodeText);
  }

  const incidentDate = getText(formData, "xincdat", "incidentDate");
  if (!/^\d{4}-\d{2}-\d{2}$/.test(incidentDate)) {
    redirectLossWithError("Enter a valid loss date.", vmfCodeText);
  }

  const lossTypeCode = getPositiveInt(formData, "xlosst", "lossTypeCode");
  if (lossTypeCode === null) {
    redirectLossWithError("Choose a loss type.", vmfCodeText);
  }

  const towNeed = getText(formData, "xtowneed", "towNeed");
  if (towNeed !== "Y" && towNeed !== "N") {
    redirectLossWithError("Choose whether a tow truck is needed.", vmfCodeText);
  }

  const informCro = getText(formData, "xcro", "informCro") || "N";
  if (informCro !== "Y" && informCro !== "N") {
    redirectLossWithError("The CLO notification choice is invalid.", vmfCodeText);
  }

  const callClosed = getText(formData, "xclosed", "callClosed") || "N";
  if (callClosed !== "Y" && callClosed !== "N") {
    redirectLossWithError("The call closed choice is invalid.", vmfCodeText);
  }

  const transportOfficerName = getText(formData, "xtrsname", "transportOfficerName");
  const transportOfficerTel = getText(formData, "xtrstel", "transportOfficerTel");
  const transportOfficerFax = getText(formData, "xtrsfax", "transportOfficerFax");
  const transportOfficerEmail = getText(formData, "xtrseml", "transportOfficerEmail");
  const callerName = getText(formData, "xcalname", "callerName");
  const callerTel = getText(formData, "xcaltel", "callerTel");
  const callerFax = getText(formData, "xcalfax", "callerFax");
  const callerEmail = getText(formData, "xcaleml", "callerEmail");
  const driverName = getText(formData, "xdrvname", "driverName");
  const driverTel = getText(formData, "xdrvtel", "driverTel");
  const driverPersalno = getText(formData, "xdrvperno", "driverPersalno");
  const croRemarks = getText(formData, "xcrem", "croRemarks");
  const suburb = getText(formData, "x1town", "suburb");
  const town = getText(formData, "x2town", "town");
  const street = getText(formData, "xstreet", "street");
  const incidentDescription = getText(formData, "xincdesc", "incidentDescription");
  const incidentRemarks = getText(formData, "xrem", "incidentRemarks");
  const placeOfLoss = [suburb, town].filter(Boolean).join(" ; ");

  validateLossMaxLength(transportOfficerName, "Transport officer name", 60, vmfCodeText);
  // The legacy Losses table stores these two values in varchar(30) and
  // varchar(20) respectively, even though the Call Centre form is wider.
  validateLossMaxLength(transportOfficerName, "Loss department contact", 30, vmfCodeText);
  validateLossMaxLength(transportOfficerTel, "Transport officer telephone", 15, vmfCodeText);
  validateLossMaxLength(transportOfficerFax, "Transport officer fax", 15, vmfCodeText);
  validateLossMaxLength(transportOfficerEmail, "Transport officer email", 30, vmfCodeText);
  validateLossMaxLength(callerName, "Caller name", 40, vmfCodeText);
  validateLossMaxLength(callerTel, "Caller telephone", 30, vmfCodeText);
  validateLossMaxLength(callerFax, "Caller fax", 15, vmfCodeText);
  validateLossMaxLength(callerEmail, "Caller email", 30, vmfCodeText);
  validateLossMaxLength(driverName, "Driver name", 60, vmfCodeText);
  validateLossMaxLength(driverName, "Loss driver name", 20, vmfCodeText);
  validateLossMaxLength(driverTel, "Driver telephone", 30, vmfCodeText);
  validateLossMaxLength(driverPersalno, "Driver personnel number", 15, vmfCodeText);
  validateLossMaxLength(croRemarks, "CLO remarks", 60, vmfCodeText);
  validateLossMaxLength(suburb, "Suburb", 30, vmfCodeText);
  validateLossMaxLength(town, "Town", 20, vmfCodeText);
  validateLossMaxLength(placeOfLoss, "Place of loss", 50, vmfCodeText);
  validateLossMaxLength(street, "Street name", 30, vmfCodeText);
  validateLossMaxLength(incidentDescription, "Loss description", 60, vmfCodeText);
  validateLossMaxLength(incidentRemarks, "Remarks", 50, vmfCodeText);
  if (informCro === "Y" && !croRemarks) {
    redirectLossWithError("Remarks for the CLO are required when informing the CLO.", String(vmfCode));
  }

  try {
    const result = await createLossIncident({
      VmfCode: vmfCode,
      IncidentType: "Loss_Theft",
      TransportOfficerName: transportOfficerName || null,
      TransportOfficerTel: transportOfficerTel || null,
      TransportOfficerFax: transportOfficerFax || null,
      TransportOfficerEmail: transportOfficerEmail || null,
      TransportOfficerSite: getPositiveInt(formData, "xtrssite", "transportOfficerSite"),
      CallerName: callerName || transportOfficerName || null,
      CallerTel: callerName ? callerTel || null : transportOfficerTel || null,
      CallerFax: callerName ? callerFax || null : transportOfficerFax || null,
      CallerEmail: callerName ? callerEmail || null : transportOfficerEmail || null,
      InformCro: informCro,
      CroRemarks: croRemarks || null,
      IncidentRemarks: incidentRemarks || null,
      NotifyListCode: getPositiveInt(formData, "xnotc", "notifyListCode"),
      CallClosed: callClosed,
      IncidentDate: `${incidentDate}T00:00:00`,
      IncidentTown: placeOfLoss || null,
      IncidentStreet: street || null,
      DriverName: driverName || null,
      DriverTel: driverTel || null,
      DriverPersalno: driverPersalno || null,
      IncidentDesc: incidentDescription || null,
      LossTypeCode: lossTypeCode,
      TowNeed: towNeed,
    });

    const params = new URLSearchParams({
      xtowneed: towNeed,
      ccVMF: String(vmfCode),
      cccode: String(result.callCentreCode),
      xinctype: "Loss_Theft",
      xgg: getText(formData, "xgg", "ggNumber"),
      xgp: getText(formData, "xgp", "registrationNumber"),
      txtDamage: "",
      lossCode: String(result.lossCode),
    });
    redirect(`${LOSS_SPLIT_PATH}?${params.toString()}`);
  } catch (error) {
    redirectLossWithError(apiErrorMessage(error), String(vmfCode));
  }
}

export async function saveLossTowingAction(formData: FormData) {
  const vmfCodeText = getText(formData, "ccVMF", "vmfCode");
  const callCentreCodeText = getText(formData, "cccode", "callCentreCode");
  await authorizeLossTowing(vmfCodeText, callCentreCodeText);

  const vmfCode = getPositiveInt(formData, "ccVMF", "vmfCode");
  const callCentreCode = getPositiveInt(formData, "cccode", "callCentreCode");
  if (vmfCode === null || callCentreCode === null) {
    redirectLossWithError(
      "The Loss/Theft reference is missing. Start the loss capture again.",
      vmfCodeText,
      LOSS_TOW_DETAIL_PATH,
      callCentreCodeText,
    );
  }

  const vehicleProblem = getText(formData, "txtDamage", "vehicleProblem");
  const towTruckCode = getPositiveInt(formData, "xtruckcod", "towTruckCode");
  const contactName = getText(formData, "xconname", "contactPersonName");
  const contactTel = getText(formData, "xcontel", "contactPersonTel");
  const contactCell = getText(formData, "xconcell", "contactPersonCell");
  const location = getText(formData, "xtown", "location");
  const remarks = getText(formData, "xrem", "remarks");

  validateLossTowMaxLength(vehicleProblem, "Vehicle problem", 60, vmfCodeText, callCentreCodeText);
  validateLossTowMaxLength(contactName, "Contact person name", 30, vmfCodeText, callCentreCodeText);
  validateLossTowMaxLength(contactTel, "Contact person telephone", 20, vmfCodeText, callCentreCodeText);
  validateLossTowMaxLength(contactCell, "Contact person cell", 10, vmfCodeText, callCentreCodeText);
  validateLossTowMaxLength(location, "Tow location", 50, vmfCodeText, callCentreCodeText);
  validateLossTowMaxLength(remarks, "Towing remarks", 50, vmfCodeText, callCentreCodeText);

  try {
    const now = new Date().toISOString();
    const towingCode = await createLossTowing({
      VmfCode: vmfCode,
      CallRefer: callCentreCode,
      RequestDate: now,
      RequestTime: now,
      Location: location || null,
      VehicleProblem: vehicleProblem || null,
      SiteCode: getPositiveInt(formData, "xtrssite", "transportOfficerSite"),
      TowTruckCode: towTruckCode,
      ContactPersonName: contactName || null,
      ContactPersonTel: contactTel || null,
      ContactPersonCell: contactCell || null,
      Remarks: remarks || null,
    });

    const params = new URLSearchParams({
      saved: "1",
      incidentType: "Loss_Theft",
      ccVMF: String(vmfCode),
      cccode: String(callCentreCode),
      code: String(callCentreCode),
      towingCode: String(towingCode),
    });
    redirect(`${LOSS_SHOW_DETAIL_PATH}?${params.toString()}`);
  } catch (error) {
    redirectLossWithError(
      apiErrorMessage(error),
      String(vmfCode),
      LOSS_TOW_DETAIL_PATH,
      String(callCentreCode),
    );
  }
}

export async function saveAccidentTowingAction(formData: FormData) {
  const vmfCodeText = getText(formData, "ccVMF", "vmfCode");
  const callCentreCodeText = getText(formData, "cccode", "callCentreCode");
  await authorizeAccidentTowing(vmfCodeText, callCentreCodeText);

  const vmfCode = getPositiveInt(formData, "ccVMF", "vmfCode");
  const callCentreCode = getPositiveInt(formData, "cccode", "callCentreCode");
  if (vmfCode === null || callCentreCode === null) {
    redirectAccidentWithError(
      "The accident reference is missing. Start the accident capture again.",
      vmfCodeText,
      ACCIDENT_TOW_DETAIL_PATH,
      callCentreCodeText,
    );
  }

  const damageDescription = getText(formData, "txtDamage", "damageDescription");
  const towTruckCode = getPositiveInt(formData, "xtruckcod", "towTruckCode");
  const contactName = getText(formData, "xconname", "contactPersonName");
  const contactTel = getText(formData, "xcontel", "contactPersonTel");
  const contactCell = getText(formData, "xconcell", "contactPersonCell");
  const location = getText(formData, "xtown", "location");
  const remarks = getText(formData, "xrem", "remarks");
  validateAccidentTowMaxLength(damageDescription, "Vehicle problem", 60, vmfCodeText, callCentreCodeText);
  validateAccidentTowMaxLength(contactName, "Contact person name", 30, vmfCodeText, callCentreCodeText);
  validateAccidentTowMaxLength(contactTel, "Contact person telephone", 20, vmfCodeText, callCentreCodeText);
  validateAccidentTowMaxLength(contactCell, "Contact person cell", 10, vmfCodeText, callCentreCodeText);
  validateAccidentTowMaxLength(location, "Tow location", 50, vmfCodeText, callCentreCodeText);
  validateAccidentTowMaxLength(remarks, "Towing remarks", 50, vmfCodeText, callCentreCodeText);

  try {
    const now = new Date().toISOString();
    const towingCode = await createAccidentTowing({
      VmfCode: vmfCode,
      CallRefer: callCentreCode,
      RequestDate: now,
      RequestTime: now,
      Location: location || null,
      VehicleProblem: damageDescription || null,
      SiteCode: getPositiveInt(formData, "xtrssite", "transportOfficerSite"),
      TowTruckCode: towTruckCode,
      ContactPersonName: contactName || null,
      ContactPersonTel: contactTel || null,
      ContactPersonCell: contactCell || null,
      Remarks: remarks || null,
    });

    const params = new URLSearchParams({
      saved: "1",
      incidentType: "Accident",
      ccVMF: String(vmfCode),
      cccode: String(callCentreCode),
      code: String(callCentreCode),
      towingCode: String(towingCode),
    });
    redirect(`${ACCIDENT_SHOW_DETAIL_PATH}?${params.toString()}`);
  } catch (error) {
    redirectAccidentWithError(
      apiErrorMessage(error),
      String(vmfCode),
      ACCIDENT_TOW_DETAIL_PATH,
      String(callCentreCode),
    );
  }
}
