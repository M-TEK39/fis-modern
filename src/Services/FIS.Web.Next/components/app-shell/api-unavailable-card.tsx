import type { ReactNode } from "react";
import Link from "next/link";

type ApiUnavailableCardProps = Readonly<{
  message: ReactNode;
  retryHref: string;
  secondaryHref?: string;
  secondaryLabel?: ReactNode;
  description?: ReactNode;
  showIcon?: boolean;
  headingLevel?: "h1" | "h2";
}>;

export default function ApiUnavailableCard({
  message,
  retryHref,
  secondaryHref,
  secondaryLabel,
  description = "The application is still running. Retry when the FIS API is available.",
  showIcon = true,
  headingLevel = "h2",
}: ApiUnavailableCardProps) {
  const Heading = headingLevel;

  return (
    <section className="vehicle-status-card" role="alert">
      {showIcon ? (
        <div className="status-icon status-icon-error" aria-hidden="true">
          !
        </div>
      ) : null}
      <p className="eyebrow">API unavailable</p>
      <Heading>{message}</Heading>
      <p className="muted-copy">{description}</p>
      <div className="button-row">
        <Link className="button button-primary" href={retryHref}>
          Try again
        </Link>
        {secondaryHref && secondaryLabel ? (
          <Link className="button button-secondary" href={secondaryHref}>
            {secondaryLabel}
          </Link>
        ) : null}
      </div>
    </section>
  );
}
