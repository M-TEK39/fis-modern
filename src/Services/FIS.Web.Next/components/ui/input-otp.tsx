"use client";

import * as React from "react";
import { OTPInput } from "input-otp";
import { cn } from "../lib/utils";
import { InputOTPGroup } from "./input-otp-group";
import { InputOTPSeparator } from "./input-otp-separator";
import { InputOTPSlot } from "./input-otp-slot";

function InputOTP({
  className,
  containerClassName,
  ref,
  ...props
}: React.ComponentPropsWithoutRef<typeof OTPInput> & {
  ref?: React.Ref<React.ElementRef<typeof OTPInput>>;
}) {
  return (
    <OTPInput
      ref={ref}
      containerClassName={cn(
        "flex items-center gap-2 has-[:disabled]:opacity-50",
        containerClassName,
      )}
      className={cn("disabled:cursor-not-allowed", className)}
      {...props}
    />
  );
}
InputOTP.displayName = "InputOTP";

export { InputOTP, InputOTPGroup, InputOTPSlot, InputOTPSeparator };
