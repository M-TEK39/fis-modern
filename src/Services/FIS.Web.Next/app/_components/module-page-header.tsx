import type { ReactNode } from "react";
import type { LucideIcon } from "lucide-react";

import { cn } from "@/components/lib/utils";

type ModulePageHeaderProps = Readonly<{
  icon: LucideIcon;
  eyebrow: string;
  title: string;
  titleId?: string;
  description?: string;
  actions?: ReactNode;
  className?: string;
}>;

/** Shared heading for a FIS module surface. */
export default function ModulePageHeader({
  icon: Icon,
  eyebrow,
  title,
  titleId,
  description,
  actions,
  className,
}: ModulePageHeaderProps) {
  return (
    <header className={cn("fis-module-page-header", className)}>
      <div className="fis-module-page-header__identity">
        <span className="fis-module-page-header__icon" aria-hidden="true">
          <Icon className="size-5" aria-hidden="true" />
        </span>
        <div>
          <p className="eyebrow">{eyebrow}</p>
          <h1 id={titleId}>{title}</h1>
          {description ? <p className="fis-module-page-header__description">{description}</p> : null}
        </div>
      </div>
      {actions ? <div className="fis-module-page-header__actions">{actions}</div> : null}
    </header>
  );
}
