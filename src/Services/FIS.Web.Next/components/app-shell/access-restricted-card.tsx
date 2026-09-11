import Link from "next/link";

type AccessRestrictedCardProps = Readonly<{
  message: string;
  href?: string;
  linkLabel?: string;
}>;

export default function AccessRestrictedCard({
  message,
  href = "/vehicles",
  linkLabel = "Back to Vehicle Master",
}: AccessRestrictedCardProps) {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>{message}</h2>
      <div className="button-row">
        <Link className="button button-secondary" href={href}>
          {linkLabel}
        </Link>
      </div>
    </section>
  );
}
