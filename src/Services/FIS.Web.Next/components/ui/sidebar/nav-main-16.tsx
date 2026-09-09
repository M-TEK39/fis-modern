"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Car,
  ChevronDown,
  CircleDot,
  LayoutDashboard,
  ShieldCheck,
  type LucideIcon,
} from "lucide-react";

import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "../collapsible";
import {
  SidebarGroup,
  SidebarGroupLabel,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarMenuSub,
  SidebarMenuSubButton,
  SidebarMenuSubItem,
  useSidebar,
} from "./sidebar";

export type NavigationItem = {
  label: string;
  href: string;
};

export type NavigationGroup = {
  label: string;
  items: readonly NavigationItem[];
};

const GROUP_ICONS: Record<string, LucideIcon> = {
  Workspace: LayoutDashboard,
  "Fleet operations": Car,
  Administration: ShieldCheck,
};

function normalizePath(value: string) {
  const normalized = value.replace(/\/+$/, "");
  return normalized || "/";
}

function isActivePath(pathname: string, href: string) {
  const activePath = normalizePath(pathname).toLocaleLowerCase();
  const targetPath = normalizePath(href).toLocaleLowerCase();

  return (
    activePath === targetPath || (targetPath !== "/" && activePath.startsWith(`${targetPath}/`))
  );
}

export function NavMain({ groups }: { groups: readonly NavigationGroup[] }) {
  const pathname = usePathname() ?? "";
  const { isMobile, setOpenMobile } = useSidebar();
  const activeGroupLabel = React.useMemo(
    () =>
      groups.find((group) => group.items.some((item) => isActivePath(pathname, item.href)))?.label,
    [groups, pathname],
  );
  const [openGroups, setOpenGroups] = React.useState<Set<string>>(() =>
    activeGroupLabel ? new Set([activeGroupLabel]) : new Set(),
  );

  React.useEffect(() => {
    setOpenGroups(activeGroupLabel ? new Set([activeGroupLabel]) : new Set());
  }, [activeGroupLabel]);

  function setGroupOpen(label: string, open: boolean) {
    setOpenGroups((current) => {
      const next = new Set(current);
      if (open) next.add(label);
      else next.delete(label);
      return next;
    });
  }

  return (
    <SidebarGroup>
      <SidebarGroupLabel>Platform</SidebarGroupLabel>
      <SidebarMenu aria-label="FIS modules">
        {groups.map((group) => {
          const GroupIcon = GROUP_ICONS[group.label] ?? CircleDot;

          return (
            <Collapsible
              key={group.label}
              asChild
              open={openGroups.has(group.label)}
              onOpenChange={(open) => setGroupOpen(group.label, open)}
              className="group/collapsible"
            >
              <SidebarMenuItem>
                <CollapsibleTrigger asChild>
                  <SidebarMenuButton tooltip={group.label} aria-label={`${group.label} section`}>
                    <GroupIcon className="size-4 shrink-0" aria-hidden="true" />
                    <span className="truncate">{group.label}</span>
                    <ChevronDown
                      className="ml-auto size-4 shrink-0 -rotate-90 transition-transform duration-200 group-data-[state=open]/collapsible:rotate-0"
                      aria-hidden="true"
                    />
                  </SidebarMenuButton>
                </CollapsibleTrigger>
                <CollapsibleContent>
                  <SidebarMenuSub>
                    {group.items.map((item) => {
                      const itemIsActive = isActivePath(pathname, item.href);

                      return (
                        <SidebarMenuSubItem key={item.href}>
                          <SidebarMenuSubButton asChild isActive={itemIsActive}>
                            <Link
                              href={item.href}
                              onClick={() => {
                                if (isMobile) setOpenMobile(false);
                              }}
                            >
                              <span>{item.label}</span>
                            </Link>
                          </SidebarMenuSubButton>
                        </SidebarMenuSubItem>
                      );
                    })}
                  </SidebarMenuSub>
                </CollapsibleContent>
              </SidebarMenuItem>
            </Collapsible>
          );
        })}
      </SidebarMenu>
    </SidebarGroup>
  );
}
