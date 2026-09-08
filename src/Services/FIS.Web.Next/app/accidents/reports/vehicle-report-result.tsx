import type { AccidentVehicleReportRow } from "@/lib/api-accidents";

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || value === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) ?? null;
}

function formatTime(value: string | null) {
  if (!value) {
    return null;
  }

  const timeStart = value.indexOf("T");
  return timeStart >= 0 ? value.slice(timeStart + 1, timeStart + 6) : value.slice(0, 5);
}

function reportFields(row: AccidentVehicleReportRow, includeAccidentCategory: boolean) {
  const fields = [
    ["Prov Reg Number", valueOrDash(row.registrationNumber)],
    ["GG Number", valueOrDash(row.fleetNumber)],
    ["Garage", valueOrDash(row.locationDescription)],
    ["Accident Date", valueOrDash(formatDate(row.accidentDate))],
    ["Accident Time", valueOrDash(formatTime(row.accidentTime))],
    ["Accident Place", valueOrDash(row.accidentPlace)],
    ["Fin year", valueOrDash(row.financialYear)],
    ["Date Updated", valueOrDash(formatDate(row.dateUpdated))],
    ["Notified Garage", valueOrDash(row.notifiedGarage)],
    ["Notified Gar Date", valueOrDash(formatDate(row.notifiedGarageDate))],
    ["Notified - Trip Auth", valueOrDash(row.notifiedTripAuthority)],
    ["Notified - Trip Auth Date", valueOrDash(formatDate(row.notifiedTripAuthorityDate))],
    ["Description of Accident", valueOrDash(row.description)],
  ];

  if (includeAccidentCategory) {
    fields.push(["Accident Category", valueOrDash(row.accidentTypeDescription)]);
  }

  fields.push(
    ["Trip Authority", valueOrDash(row.tripAuthority)],
    ["Driver Name", valueOrDash(row.driverName)],
    ["Driver ID Number", valueOrDash(row.driverEmployNumber)],
    ["Driver Site", valueOrDash(row.departmentNumber)],
    ["Transport Officer", valueOrDash(row.transportOfficerName)],
    ["Transport Officer Tel", valueOrDash(row.transportOfficerTelephone)],
    ["HQ Reference", valueOrDash(row.hqReference)],
    ["GG Reference", valueOrDash(row.ggReference)],
    ["SA Reference", valueOrDash(row.saReference)],
    ["Case Number", valueOrDash(row.caseNumber)],
    ["Job Number", valueOrDash(row.caseNumber)],
    ["GG Car Damage Amount", valueOrDash(row.costOfRepair)],
    ["GG Car Damage Desc", valueOrDash(row.damageDescription)],
    ["Driver Fault", valueOrDash(row.driverFault)],
    ["Death", valueOrDash(row.death)],
    ["Injured", valueOrDash(row.injured)],
    ["Private Party Regno", valueOrDash(row.thirdPartyRegistration)],
    ["Third Party Owner", valueOrDash(row.thirdPartyOwner)],
    ["Private Car Damage", valueOrDash(row.thirdPartyClaim)],
    ["Priv Damage Pay Date", valueOrDash(formatDate(row.privateDamagePaymentDate))],
    ["Claim Against Dept", valueOrDash(row.insuranceClaim)],
    ["Claim Received", valueOrDash(row.claimReceived)],
    ["Cost Claim Agains Dept", valueOrDash(row.claimAgainstDepartment)],
    ["Claim Accept/Reject", valueOrDash(row.claimDecision)],
    ["Claim Reject Reason", valueOrDash(row.claimRejectReason)],
    ["Write Off Amount", valueOrDash(row.writeOffAmount)],
    ["Write Off Date", valueOrDash(formatDate(row.writeOffDate))],
    ["LetterHead", valueOrDash(row.letterhead)],
    ["Z181", valueOrDash(row.z181)],
    ["File Close Date", valueOrDash(formatDate(row.fileCloseDate))],
    ["Notes", valueOrDash(row.notes)],
  );

  return fields;
}

export default function VehicleReportResult({
  row,
  index,
  includeAccidentCategory = true,
}: {
  row: AccidentVehicleReportRow;
  index: number;
  includeAccidentCategory?: boolean;
}) {
  const titleId = `vehicle-accident-report-${row.accidentCode}`;
  return (
    <article className="vehicle-status-maintenance-panel" aria-labelledby={titleId}>
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Accident {index + 1}</p>
          <h2 id={titleId}>Record {row.accidentCode}</h2>
        </div>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">
            Detailed accident report for record {row.accidentCode}
          </caption>
          <tbody>
            {reportFields(row, includeAccidentCategory).map(([label, value]) => (
              <tr key={label}>
                <th scope="row">{label}</th>
                <td>{value}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </article>
  );
}
