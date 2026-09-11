import Link from "next/link";

type ReportMenuLink = readonly [mode: string, title: string, description: string];

type ReportsMenuLayoutProps = Readonly<{
  headingId: string;
  eyebrow: string;
  title: string;
  description: string;
  backHref: string;
  linkBase: string;
  links: readonly ReportMenuLink[];
}>;

export default function ReportsMenuLayout({
  headingId,
  eyebrow,
  title,
  description,
  backHref,
  linkBase,
  links,
}: ReportsMenuLayoutProps) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby={headingId}>
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">{eyebrow}</p>
            <h1 id={headingId}>{title}</h1>
            <p>{description}</p>
          </div>
          <Link className="button button-secondary" href={backHref}>
            Back
          </Link>
        </header>
        <div className="vehicle-menu-tiles">
          {links.map(([mode, linkTitle, linkDescription]) => (
            <section className="vehicle-menu-tile" key={mode}>
              <h2 className="vehicle-menu-header">
                <Link href={`${linkBase}/${mode}`}>{linkTitle}</Link>
              </h2>
              <div className="vehicle-menu-body">
                <p className="muted-copy">{linkDescription}</p>
                <Link className="button button-primary button-small" href={`${linkBase}/${mode}`}>
                  Open report
                </Link>
              </div>
            </section>
          ))}
        </div>
      </section>
    </main>
  );
}
