"use client";

import { useEffect, useMemo, useState } from "react";
import { Bell, CheckCheck, Command as CommandIcon, Monitor, Moon, Search, Sun } from "lucide-react";
import { useTheme } from "next-themes";
import { usePathname, useRouter } from "next/navigation";
import Link from "next/link";

import { Button } from "@/components/ui/button";
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandShortcut,
} from "@/components/ui/command";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet";
import { SidebarTrigger } from "@/components/ui/sidebar/sidebar";
import { Separator } from "@/components/ui/separator";
import {
  Breadcrumb,
  BreadcrumbList,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbSeparator,
  BreadcrumbPage,
} from "@/components/ui/breadcrumb";

type NavigationItem = Readonly<{
  label: string;
  href: string;
}>;

type NavigationGroup = Readonly<{
  label: string;
  items: readonly NavigationItem[];
}>;

type ThemeMode = "light" | "dark" | "system";

function ThemeModeIcon({ mode }: Readonly<{ mode: ThemeMode }>) {
  if (mode === "light") return <Sun aria-hidden="true" />;
  if (mode === "dark") return <Moon aria-hidden="true" />;
  return <Monitor aria-hidden="true" />;
}

function HeaderSearch({ groups }: Readonly<{ groups: readonly NavigationGroup[] }>) {
  const [open, setOpen] = useState(false);
  const pathname = usePathname();
  const router = useRouter();

  const commandGroups = useMemo(() => groups.filter((group) => group.items.length > 0), [groups]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const target = event.target;
      const editing =
        target instanceof HTMLElement &&
        (target.isContentEditable ||
          target.tagName === "INPUT" ||
          target.tagName === "TEXTAREA" ||
          target.tagName === "SELECT");

      if (editing) return;

      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setOpen(true);
      }
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  function navigate(href: string) {
    setOpen(false);
    router.push(href);
  }

  return (
    <>
      <Button
        type="button"
        variant="outline"
        size="sm"
        onClick={() => setOpen(true)}
        className="hidden min-w-52 justify-start gap-2 text-muted-foreground md:inline-flex lg:min-w-72"
        aria-label="Search available modules"
      >
        <Search aria-hidden="true" />
        <span>Search modules...</span>
        <CommandShortcut className="ml-auto normal-case tracking-normal">Ctrl K</CommandShortcut>
      </Button>
      <Button
        type="button"
        variant="ghost"
        size="icon"
        onClick={() => setOpen(true)}
        className="md:hidden"
        aria-label="Search available modules"
      >
        <Search aria-hidden="true" />
      </Button>
      <CommandDialog open={open} onOpenChange={setOpen}>
        <CommandInput placeholder="Search the modules available to you..." />
        <CommandList>
          <CommandEmpty>No available module matches that search.</CommandEmpty>
          {commandGroups.map((group) => (
            <CommandGroup key={group.label} heading={group.label}>
              {group.items.map((item) => (
                <CommandItem
                  key={item.href}
                  value={`${group.label} ${item.label}`}
                  onSelect={() => navigate(item.href)}
                  aria-current={pathname === item.href ? "page" : undefined}
                >
                  <CommandIcon aria-hidden="true" />
                  <span>{item.label}</span>
                </CommandItem>
              ))}
            </CommandGroup>
          ))}
        </CommandList>
      </CommandDialog>
    </>
  );
}

function HeaderThemeMenu() {
  const { setTheme, theme } = useTheme();
  const [mounted, setMounted] = useState(false);

  useEffect(() => setMounted(true), []);

  // next-themes reads the persisted preference in the browser. Keep the first
  // client render on the same system icon/value as SSR, then reveal that
  // preference after hydration to avoid mismatching the Radix trigger markup.
  const activeTheme: ThemeMode =
    mounted && (theme === "light" || theme === "dark") ? theme : "system";

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button type="button" variant="ghost" size="icon" aria-label="Choose colour mode">
          <ThemeModeIcon mode={activeTheme} />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="min-w-40">
        <DropdownMenuRadioGroup value={activeTheme} onValueChange={(mode) => setTheme(mode)}>
          <DropdownMenuRadioItem value="light">
            <Sun aria-hidden="true" />
            Light
          </DropdownMenuRadioItem>
          <DropdownMenuRadioItem value="dark">
            <Moon aria-hidden="true" />
            Dark
          </DropdownMenuRadioItem>
          <DropdownMenuRadioItem value="system">
            <Monitor aria-hidden="true" />
            System
          </DropdownMenuRadioItem>
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function HeaderNotifications({
  noticeManagementHref,
}: Readonly<{ noticeManagementHref?: string }>) {
  return (
    <Sheet>
      <SheetTrigger asChild>
        <Button type="button" variant="ghost" size="icon" aria-label="Open notifications">
          <Bell aria-hidden="true" />
        </Button>
      </SheetTrigger>
      <SheetContent side="right" className="flex flex-col">
        <SheetHeader>
          <SheetTitle>Notifications</SheetTitle>
          <SheetDescription>
            Review scheduled notices now; a personal notification inbox will appear here when it is
            available.
          </SheetDescription>
        </SheetHeader>
        <div className="flex flex-1 flex-col items-center justify-center gap-3 text-center">
          <div className="rounded-full bg-muted p-3 text-muted-foreground">
            <CheckCheck aria-hidden="true" />
          </div>
          <div className="space-y-1">
            <p className="font-medium text-foreground">Notification inbox unavailable</p>
            <p className="text-sm text-muted-foreground">
              No per-user notification feed is configured for this workspace yet.
            </p>
          </div>
          {noticeManagementHref ? (
            <Button asChild variant="outline" size="sm">
              <Link href={noticeManagementHref}>Manage scheduled notices</Link>
            </Button>
          ) : null}
        </div>
      </SheetContent>
    </Sheet>
  );
}

export default function SiteHeader({ groups }: Readonly<{ groups: readonly NavigationGroup[] }>) {
  const pathname = usePathname();
  const noticeManagementHref = groups
    .flatMap((group) => group.items)
    .find((item) => item.href === "/notice-management")?.href;
  const section = (pathname.split("/").filter(Boolean)[0] ?? "home")
    .replace(/-/g, " ")
    .replace(/\b\w/g, (letter) => letter.toUpperCase());
  return (
    <header className="sticky top-0 z-20 flex h-[--header-height] shrink-0 items-center gap-2 border-b bg-background">
      <div className="flex w-full items-center gap-2 px-4">
        <SidebarTrigger aria-label="Toggle navigation" />
        <Separator orientation="vertical" className="mx-2 h-4" />
        <Breadcrumb>
          <BreadcrumbList>
            <BreadcrumbItem className="hidden sm:block">
              <BreadcrumbLink href="/home">Fleet Information System</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator className="hidden sm:block" />
            <BreadcrumbItem>
              <BreadcrumbPage>{section}</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>
        <div className="ml-auto flex items-center gap-1">
          <HeaderSearch groups={groups} />
          <HeaderThemeMenu />
          <HeaderNotifications noticeManagementHref={noticeManagementHref} />
        </div>
      </div>
    </header>
  );
}
