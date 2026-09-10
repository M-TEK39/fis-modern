"use client";

import * as React from "react";
import Link from "next/link";
import Image from "next/image";
import { CircleHelp } from "lucide-react";

import { NavMain } from "./nav-main-16";
import { NavSecondary } from "./nav-secondary-16";
import { NavUser } from "./nav-user-16";
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
} from "./sidebar";

type NavigationItem = {
  label: string;
  href: string;
};

type NavigationGroup = {
  label: string;
  items: readonly NavigationItem[];
};

type SidebarUser = {
  name: string;
  email: string;
  initials: string;
  avatar: string | null;
};

type LogoutAction = (formData: FormData) => void | Promise<void>;

export function AppSidebar16({
  groups,
  user,
  canManageSystemSettings,
  logoutAction,
  ...props
}: React.ComponentProps<typeof Sidebar> & {
  groups: readonly NavigationGroup[];
  user: SidebarUser;
  canManageSystemSettings: boolean;
  logoutAction: LogoutAction;
}) {
  return (
    <Sidebar className="top-[--header-height] !h-[calc(100svh-var(--header-height))]" {...props}>
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" asChild>
              <Link href="/home">
                <div className="flex aspect-square size-8 items-center justify-center overflow-hidden rounded-lg bg-white p-0.5 ring-1 ring-sidebar-border">
                  <Image
                    src="/logo/gauteng-g-fleet.webp"
                    alt=""
                    width={32}
                    height={32}
                    className="size-full object-contain"
                  />
                </div>
                <div className="grid flex-1 text-left text-sm leading-tight">
                  <span className="truncate font-semibold">Fleet Information System</span>
                  <span className="truncate text-xs">Gauteng Provincial Government</span>
                </div>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>
      <SidebarContent>
        <NavMain groups={groups} />
        <NavSecondary
          items={[{ title: "Help & manuals", url: "/manuals", icon: CircleHelp }]}
          className="mt-auto"
        />
      </SidebarContent>
      <SidebarFooter>
        <NavUser
          user={user}
          canManageSystemSettings={canManageSystemSettings}
          logoutAction={logoutAction}
          helpHref="/manuals"
        />
      </SidebarFooter>
    </Sidebar>
  );
}
