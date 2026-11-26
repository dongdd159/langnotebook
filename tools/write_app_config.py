#!/usr/bin/env python3
"""Writes LangNotebook/wwwroot/config/app.config.json for a deployment, e.g. from CI secrets.

It copies the template docs/app.config.example.json and fills in values from the environment or --set options, so the
real file never has to be committed:

    LANGNOTEBOOK_DROPBOX_CLIENT_ID=abc LANGNOTEBOOK_GOOGLE_CLIENT_ID=xyz python3 tools/write_app_config.py
    python3 tools/write_app_config.py --set cloud.dropbox.clientId=abc --set ai.anthropic.apiVersion=2026-10-21

Run it before `dotnet publish`. Values end up in a file served with the app, so they are visible to anyone who opens the
site: use it for public identifiers such as client IDs, not for secrets.
"""
import argparse
import json
import os
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CONFIG_DIR = ROOT / "LangNotebook" / "wwwroot" / "config"
TEMPLATE = ROOT / "docs" / "app.config.example.json"
ENVIRONMENT = {
    "LANGNOTEBOOK_DROPBOX_CLIENT_ID": "cloud.dropbox.clientId",
    "LANGNOTEBOOK_GOOGLE_CLIENT_ID": "cloud.google.clientId",
}


def set_path(document: dict, dotted: str, value: str) -> None:
    keys = dotted.split(".")
    node = document
    for key in keys[:-1]:
        child = node.get(key)
        if not isinstance(child, dict):
            raise SystemExit(f"error: '{dotted}' does not exist in the example file (no section '{key}')")
        node = child
    if keys[-1] not in node:
        raise SystemExit(f"error: '{dotted}' does not exist in the example file")
    if not isinstance(node[keys[-1]], str):
        raise SystemExit(f"error: '{dotted}' is not a text value")
    node[keys[-1]] = value


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--example", type=Path, default=TEMPLATE)
    parser.add_argument("-o", "--output", type=Path, default=CONFIG_DIR / "app.config.json")
    parser.add_argument("--set", action="append", default=[], metavar="PATH=VALUE",
                        help="set a text value, for example cloud.dropbox.clientId=abc (repeatable)")
    args = parser.parse_args()

    document = json.loads(args.example.read_text(encoding="utf-8"))
    document.pop("_readme", None)
    applied = []
    for variable, dotted in ENVIRONMENT.items():
        value = os.environ.get(variable, "").strip()
        if value:
            set_path(document, dotted, value)
            applied.append(dotted)
    for item in args.set:
        dotted, separator, value = item.partition("=")
        if not separator:
            raise SystemExit(f"error: --set expects PATH=VALUE, got '{item}'")
        set_path(document, dotted.strip(), value.strip())
        applied.append(dotted.strip())

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(document, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"wrote {args.output} ({len(applied)} value(s) set: {', '.join(applied) or 'none, defaults only'})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
