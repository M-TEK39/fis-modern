"use client";

import { useId, useState, type ReactNode } from "react";
import { ChevronDown } from "lucide-react";

import { cn } from "../lib/utils";
import { Button } from "./button";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "./collapsible";

type MenuSectionProps = Readonly<{
  title: string;
  children: ReactNode;
  defaultOpen?: boolean;
}>;

export function MenuSection({ title, children, defaultOpen = false }: MenuSectionProps) {
  const [open, setOpen] = useState(defaultOpen);
  const headingId = useId();
  const contentId = `${headingId}-content`;

  return (
    <Collapsible
      className="vehicle-menu-tile group/menu-section overflow-hidden"
      data-slot="menu-section"
      open={open}
      onOpenChange={setOpen}
    >
      <h2 className="m-0 text-base font-medium" id={headingId}>
        <CollapsibleTrigger asChild>
          <Button
            type="button"
            variant="ghost"
            className="h-auto min-h-11 w-full justify-between rounded-none px-4 py-3 text-left text-sm font-medium text-foreground hover:bg-secondary/70 hover:text-foreground"
            aria-controls={contentId}
          >
            <span>{title}</span>
            <ChevronDown
              className={cn(
                "size-4 shrink-0 transition-transform duration-200",
                open && "rotate-180",
              )}
              aria-hidden="true"
            />
          </Button>
        </CollapsibleTrigger>
      </h2>
      <CollapsibleContent id={contentId}>
        <div className="vehicle-menu-body">{children}</div>
      </CollapsibleContent>
    </Collapsible>
  );
}
