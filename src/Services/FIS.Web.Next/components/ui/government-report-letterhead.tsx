type GovernmentReportLetterheadProps = Readonly<{
  title?: string;
  variant?: "department" | "garage";
  reference?: string;
  governmentMotorTransport?: boolean;
  printOnly?: boolean;
}>;

/**
 * The masthead used by the legacy paper forms. Keep this separate from the
 * g-FleeT product logo: these documents are issued on Gauteng Provincial
 * Government Roads and Transport stationery.
 */
export default function GovernmentReportLetterhead({
  title,
  variant = "department",
  reference,
  governmentMotorTransport = false,
  printOnly = false,
}: GovernmentReportLetterheadProps) {
  return (
    <header
      className={`government-report-letterhead${printOnly ? " government-report-letterhead-print-only" : ""}`}
    >
      <img
        alt="Gauteng Province Roads and Transport, Republic of South Africa"
        className="government-report-letterhead-logo"
        height={121}
        src="/logo/gauteng-province-roads-and-transport.jpg"
        width={389}
      />
      <div className="government-report-letterhead-copy">
        {variant === "garage" ? (
          <>
            <p className="government-report-letterhead-title">
              GOVERNMENT GARAGE - STAATSGARAGE : JOHANNESBURG
            </p>
            <p>16 BOEINGSTR. EAST, BEDFORDVIEW, PRIVATE BAG X1 BEDFORDVIEW 2008</p>
            <p>
              <strong>ENQUIRIES:</strong> M. Abbott
              {reference ? (
                <>
                  {" "}
                  <strong>Ref Number:</strong> {reference}
                </>
              ) : null}
            </p>
          </>
        ) : (
          <>
            {governmentMotorTransport ? (
              <p className="government-report-letterhead-title">GOVERNMENT MOTOR TRANSPORT</p>
            ) : null}
            <p className="government-report-letterhead-title">
              DEPARTMENT OF PUBLIC TRANSPORT, ROADS AND WORKS
            </p>
            <p>DEPARTEMENT VAN OPENBARE VERVOER, PAAIE EN WERKE</p>
            <p>LEFAPHA DIPALANGWA TSA SETJHABA, DITSELA LE MESEBTSI</p>
            <p>UMNYANGO WEZOKUTHUTHA WOMPHAKATHI, EZEMIGWAQO NEZEMSEBENZI</p>
          </>
        )}
      </div>
      {title ? <h2 className="government-report-letterhead-document-title">{title}</h2> : null}
    </header>
  );
}
