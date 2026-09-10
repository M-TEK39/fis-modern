export const metadata = {
  title: "Terms & Privacy",
};

const sections = [
  {
    id: "corporate-terms",
    kicker: "Corporate",
    title: "Terms & Conditions",
    paragraphs: [
      "The Fleet Information System is reserved for authorized Gauteng Provincial Government personnel and accredited contractors. Access is granted strictly for business operations and is subject to continuous monitoring and audit.",
      "By using this system, you agree to uphold data integrity, comply with fleet governance standards, and follow operational directives issued by fleet management leadership.",
    ],
  },
  {
    id: "acceptable-use",
    kicker: "Acceptable Use",
    title: "Operational Boundaries",
    paragraphs: [
      "Access to fleet records, trip authorities, incident reports, and financial information must align with your assigned duties. Any access outside of assigned responsibilities requires written authorization from your line manager.",
      "Do not export, print, or distribute system data through unapproved channels. All data handling must follow departmental information governance procedures.",
    ],
  },
  {
    id: "privacy-policy",
    kicker: "Privacy",
    title: "Privacy Policy",
    paragraphs: [
      "The system records operational data including vehicle usage, incident details, audit logs, and user activity. These records are used for compliance, reporting, and service delivery oversight.",
      "Personal information is restricted to authorized roles and processed in accordance with public sector privacy obligations and internal compliance guidelines.",
    ],
  },
  {
    id: "data-retention",
    kicker: "Retention",
    title: "Data Retention & Audit",
    paragraphs: [
      "Fleet records are retained for the period required by corporate audit policies. Access logs, approvals, and exception reports may be reviewed by compliance officers.",
      "Report exports and operational summaries are archived as official records. Unauthorized modifications or deletions are prohibited and subject to disciplinary action.",
    ],
  },
  {
    id: "contact",
    kicker: "Support",
    title: "Contact & Support",
    paragraphs: [
      "For policy clarification, reach out to your departmental compliance officer or the fleet systems administrator. Requests for access changes must be submitted through the approved governance workflow.",
    ],
  },
] as const;

export default function TermsPage() {
  return (
    <main className="page-shell terms-page">
      <header className="terms-page-header">
        <p className="eyebrow">Fleet Information System</p>
        <h1>Terms &amp; Policies</h1>
        <p>Internal policies for fleet data, privacy, and acceptable use across all departments.</p>
      </header>

      <div className="terms-layout">
        <aside className="terms-nav" aria-label="Terms sections">
          <div className="terms-nav-header">On this page</div>
          {sections.map((section) => (
            <a className="terms-nav-link" href={`#${section.id}`} key={section.id}>
              {section.title === "Terms & Conditions" ? "Corporate Terms" : section.title}
            </a>
          ))}
        </aside>

        <div className="terms-content">
          {sections.map((section) => (
            <section className="terms-section" id={section.id} key={section.id}>
              <div className="terms-kicker">{section.kicker}</div>
              <h2>{section.title}</h2>
              {section.paragraphs.map((paragraph) => (
                <p key={paragraph}>{paragraph}</p>
              ))}
            </section>
          ))}
        </div>
      </div>
    </main>
  );
}
