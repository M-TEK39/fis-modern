import Link from "next/link";

import { AuthBrand, AuthHeader, AuthPage } from "@/components/app-shell/auth-ui";
import { Button } from "@/components/ui/button";

export default function SignedOutPage() {
  return (
    <AuthPage>
      <AuthBrand />
      <AuthHeader
        id="signed-out-title"
        title="You've been signed out"
        description="Your session has been ended successfully."
      />

      <div className="flex flex-col gap-3">
        <Button asChild className="w-full">
          <Link href="/login">Sign in again</Link>
        </Button>
        <Button asChild className="w-full" variant="outline">
          <Link href="/">Return to home</Link>
        </Button>
      </div>
    </AuthPage>
  );
}
