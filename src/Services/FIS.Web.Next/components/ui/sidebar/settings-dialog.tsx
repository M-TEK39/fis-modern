"use client";

import * as React from "react";
import NextLink from "next/link";
import { CircleHelp, Lock, Paintbrush, type LucideIcon } from "lucide-react";
import { useTheme } from "next-themes";

import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "../breadcrumb";
import { Button } from "../button";
import { Dialog, DialogContent, DialogDescription, DialogTitle } from "../dialog";
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
} from "./sidebar";

type SettingsItem = {
  name: string;
  icon: LucideIcon;
  description: string;
  href?: string;
};

const data: { nav: readonly SettingsItem[] } = {
  nav: [
    {
      name: "Appearance",
      icon: Paintbrush,
      description: "Choose how the workspace looks on this device.",
    },
    {
      name: "Security",
      icon: Lock,
      description: "Update your password to keep your account secure.",
      href: "/change-password",
    },
    {
      name: "Help & manuals",
      icon: CircleHelp,
      description: "Find guidance for your fleet workflows.",
      href: "/manuals",
    },
  ],
};

export function SettingsDialog({
  open,
  onOpenChange,
  onCloseAutoFocus,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCloseAutoFocus?: (event: Event) => void;
}) {
  const [section, setSection] = React.useState("Appearance");
  const [mounted, setMounted] = React.useState(false);
  const { setTheme, theme } = useTheme();
  const activeItem = data.nav.find((item) => item.name === section) ?? data.nav[0];

  React.useEffect(() => setMounted(true), []);

  React.useEffect(() => {
    if (!open) setSection("Appearance");
  }, [open]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        onCloseAutoFocus={onCloseAutoFocus}
        className="h-[min(90svh,32rem)] overflow-hidden p-0 sm:max-w-[700px] lg:max-w-[800px]"
      >
        <DialogTitle className="sr-only">FIS settings</DialogTitle>
        <DialogDescription className="sr-only">
          Review available FIS account, navigation, security, and help options.
        </DialogDescription>
        <SidebarProvider className="h-full min-h-0 items-start">
          <Sidebar collapsible="none" className="hidden md:flex">
            <SidebarContent>
              <SidebarGroup>
                <SidebarGroupContent>
                  <SidebarMenu>
                    {data.nav.map((item) => (
                      <SidebarMenuItem key={item.name}>
                        <SidebarMenuButton asChild isActive={item.name === activeItem.name}>
                          <button type="button" onClick={() => setSection(item.name)}>
                            <item.icon />
                            <span>{item.name}</span>
                          </button>
                        </SidebarMenuButton>
                      </SidebarMenuItem>
                    ))}
                  </SidebarMenu>
                </SidebarGroupContent>
              </SidebarGroup>
            </SidebarContent>
          </Sidebar>
          <main className="flex min-h-0 flex-1 flex-col overflow-hidden">
            <header className="flex h-16 shrink-0 items-center gap-2 transition-[width,height] ease-linear group-has-[[data-collapsible=icon]]/sidebar-wrapper:h-12">
              <div className="flex items-center gap-2 px-4">
                <Breadcrumb>
                  <BreadcrumbList>
                    <BreadcrumbItem className="hidden md:block">
                      <BreadcrumbPage>Settings</BreadcrumbPage>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator className="hidden md:block" />
                    <BreadcrumbItem>
                      <BreadcrumbPage>{activeItem.name}</BreadcrumbPage>
                    </BreadcrumbItem>
                  </BreadcrumbList>
                </Breadcrumb>
              </div>
            </header>
            <div className="flex gap-1 overflow-x-auto border-y px-4 py-2 md:hidden">
              {data.nav.map((item) => (
                <button
                  key={item.name}
                  type="button"
                  className="shrink-0 rounded-md px-3 py-2 text-xs font-medium text-muted-foreground hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  aria-current={item.name === activeItem.name ? "page" : undefined}
                  onClick={() => setSection(item.name)}
                >
                  {item.name}
                </button>
              ))}
            </div>
            <div className="flex flex-1 flex-col overflow-y-auto p-4 pt-0 sm:p-6 sm:pt-0">
              <section className="mx-auto w-full max-w-2xl space-y-6" aria-live="polite">
                <div className="space-y-2 border-b pb-5 pt-5 sm:pt-6">
                  <h2 className="text-xl font-semibold tracking-tight">{activeItem.name}</h2>
                  <p className="text-sm text-muted-foreground">{activeItem.description}</p>
                </div>
                {activeItem.href ? (
                  <Button asChild>
                    <NextLink href={activeItem.href} onClick={() => onOpenChange(false)}>
                      {activeItem.name === "Security" ? "Change password" : "Open manuals"}
                    </NextLink>
                  </Button>
                ) : activeItem.name === "Appearance" ? (
                  <div className="space-y-3">
                    <p className="text-sm text-muted-foreground">
                      Use a light or dark theme, or follow your device settings.
                    </p>
                    <div className="flex flex-wrap gap-2" aria-label="Theme preference">
                      {(["light", "dark", "system"] as const).map((option) => (
                        <Button
                          key={option}
                          type="button"
                          variant={mounted && theme === option ? "default" : "outline"}
                          aria-pressed={mounted && theme === option}
                          onClick={() => setTheme(option)}
                        >
                          {option[0].toUpperCase() + option.slice(1)}
                        </Button>
                      ))}
                    </div>
                  </div>
                ) : null}
              </section>
            </div>
          </main>
        </SidebarProvider>
      </DialogContent>
    </Dialog>
  );
}
