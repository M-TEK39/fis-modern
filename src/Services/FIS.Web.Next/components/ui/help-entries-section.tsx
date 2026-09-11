import DataTableHeader from "@/components/ui/data-table-header";

type HelpEntry = Readonly<{
  term: string;
  description: string;
}>;

type HelpEntriesSectionProps = Readonly<{
  headingId: string;
  description: string;
  caption: string;
  entries: readonly HelpEntry[];
}>;

export default function HelpEntriesSection({
  headingId,
  description,
  caption,
  entries,
}: HelpEntriesSectionProps) {
  return (
    <section className="module-help-section" aria-labelledby={headingId}>
      <h2 id={headingId}>Term / Field Analysis</h2>
      <p>{description}</p>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table module-help-table">
          <caption className="sr-only">{caption}</caption>
          <DataTableHeader
            columns={[
              { key: "term", label: "Term / Field" },
              { key: "definition", label: "Definition" },
            ]}
          />
          <tbody>
            {entries.map((entry) => (
              <tr key={entry.term}>
                <th scope="row">{entry.term}</th>
                <td>{entry.description}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
