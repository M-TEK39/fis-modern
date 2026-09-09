"use client";

import { BookOpen, ChevronDown, FileText } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";

export type ManualLink = {
  label: string;
  href: string;
};

export type ManualGroup = {
  title: string;
  links: readonly ManualLink[];
};

export function ManualLibrary({ groups }: Readonly<{ groups: readonly ManualGroup[] }>) {
  return (
    <div className="grid gap-3" aria-label="User manuals">
      {groups.map((group) => (
        <Collapsible className="group" key={group.title}>
          <Card className="overflow-hidden shadow-none">
            <CollapsibleTrigger asChild>
              <Button
                className="h-auto w-full justify-between rounded-none px-5 py-4 text-left text-base font-semibold hover:bg-secondary focus-visible:ring-inset"
                type="button"
                variant="ghost"
              >
                <span className="flex items-center gap-3">
                  <span className="flex size-8 items-center justify-center rounded-md bg-secondary text-secondary-foreground">
                    <BookOpen className="size-4" aria-hidden="true" />
                  </span>
                  {group.title}
                </span>
                <ChevronDown
                  className="size-4 shrink-0 transition-transform group-data-[state=open]:rotate-180"
                  aria-hidden="true"
                />
              </Button>
            </CollapsibleTrigger>
            <CollapsibleContent className="border-t">
              <div className="grid gap-1 p-2">
                {group.links.map((link) => (
                  <a
                    className="flex min-h-11 items-center gap-3 rounded-md px-3 py-2 text-sm font-medium text-foreground transition-colors hover:bg-secondary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                    href={link.href}
                    key={link.href}
                  >
                    <FileText className="size-4 text-muted-foreground" aria-hidden="true" />
                    {link.label}
                  </a>
                ))}
              </div>
            </CollapsibleContent>
          </Card>
        </Collapsible>
      ))}
    </div>
  );
}
