#!/usr/bin/env python3
from pathlib import Path
import re


def add_authorize(controller_path: Path) -> bool:
    text = controller_path.read_text(encoding="utf-8")
    if "[Authorize" in text or "[AllowAnonymous" in text:
        return False

    if "Microsoft.AspNetCore.Authorization" not in text:
        lines = text.splitlines()
        insert_at = 0
        for i, line in enumerate(lines):
            if line.startswith("using "):
                insert_at = i + 1
            else:
                break
        lines.insert(insert_at, "using Microsoft.AspNetCore.Authorization;")
        text = "\n".join(lines)

    if "[Route(" in text:
        text = re.sub(r"(\n\[Route\([^)]+\)\])", r"\n[Authorize]\1", text, count=1)
    elif "[ApiController]" in text:
        text = text.replace("[ApiController]", "[ApiController]\n[Authorize]", 1)
    else:
        text = re.sub(r"(public class \w+Controller)", r"[Authorize]\n\1", text, count=1)

    controller_path.write_text(text, encoding="utf-8")
    return True


def main() -> None:
    root = Path(__file__).resolve().parents[1] / "src" / "Services" / "FIS.Api" / "Controllers"
    changed = []
    for path in sorted(root.glob("*.cs")):
        if add_authorize(path):
            changed.append(path.name)

    if changed:
        print("Added [Authorize] to:")
        for name in changed:
            print(f"- {name}")
    else:
        print("No controllers needed updates.")


if __name__ == "__main__":
    main()
