import * as React from "react";

import { cn } from "../lib/utils";
import { AvatarFallback } from "./avatar-fallback";
import { AvatarImage } from "./avatar-image";

function Avatar({ className, ...props }: React.ComponentProps<"span">) {
  return (
    <span
      className={cn("relative flex h-10 w-10 shrink-0 overflow-hidden rounded-full", className)}
      {...props}
    />
  );
}

export { Avatar, AvatarFallback, AvatarImage };
