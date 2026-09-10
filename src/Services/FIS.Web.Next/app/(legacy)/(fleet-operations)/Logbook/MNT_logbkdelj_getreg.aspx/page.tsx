import { redirect } from "next/navigation";

export default function LegacyLogbookDeleteAlias() {
  redirect("/log-books/delete");
}
