import type { ReactNode } from "react";

import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar/sidebar";
import { getAppShellData } from "./app-shell-data";

// Kept as a compatibility export until the app layout can import shell data
// from its non-component module directly.
export { getAppShellData } from "./app-shell-data";

export function AppShellFrame({
  children,
  header,
  sidebar,
}: Readonly<{ children: ReactNode; header: ReactNode; sidebar: ReactNode }>) {
  return (
    <SidebarProvider className="flex-col bg-background [--header-height:3.5rem]">
      {header}
      <div className="flex min-h-0 flex-1">
        {sidebar}
        <SidebarInset>
          <div className="fis-app-content flex min-w-0 flex-1 flex-col gap-4 p-4 md:p-6">
            {children}
          </div>
        </SidebarInset>
      </div>
    </SidebarProvider>
  );
}
