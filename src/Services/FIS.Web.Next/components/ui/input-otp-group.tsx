"use client";

import * as React from "react";

import { cn } from "../lib/utils";

function InputOTPGroup({
  className,
  ref,
  ...props
}: React.ComponentPropsWithoutRef<"div"> & { ref?: React.Ref<React.ElementRef<"div">> }) {
  return <div ref={ref} className={cn("flex items-center", className)} {...props} />;
}
InputOTPGroup.displayName = "InputOTPGroup";

export { InputOTPGroup };
