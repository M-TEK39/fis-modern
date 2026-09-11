import type { ReactNode } from "react";
import Link from "next/link";

type StatusCardProps = Readonly<{
  title: ReactNode;
  message: ReactNode;
  description?: ReactNode;
  retryHref?: string;
  retryLabel?: ReactNode;
  secondaryHref?: string;
  secondaryLabel?: ReactNode;
  showIcon?: boolean;
}>;

export default function StatusCard({
  title,
  message,
  description,
  retryHref,
  retryLabel = "Try again",
  secondaryHref,
  secondaryLabel,
  showIcon = false,
}: StatusCardProps) {
  return (
    <section className="vehicle-status-card" role="alert">
      {showIcon ? (
        <div className="status-icon status-icon-error" aria-hidden="true">
          !
        </div>
      ) : null}
      <p className="eyebrow">{title}</p>
      <h2>{message}</h2>
      {description ? <p className="muted-copy">{description}</p> : null}
      {retryHref || secondaryHref ? (
        <div className="button-row">
          {retryHref ? (
            <Link className="button button-primary" href={retryHref}>
              {retryLabel}
            </Link>
          ) : null}
          {secondaryHref && secondaryLabel ? (
            <Link className="button button-secondary" href={secondaryHref}>
              {secondaryLabel}
            </Link>
          ) : null}
        </div>
      ) : null}
    </section>
  );
}
