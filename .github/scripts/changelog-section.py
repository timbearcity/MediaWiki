#!/usr/bin/env python3
"""Print the CHANGELOG.md section for one version, for use as GitHub release notes.

Exits non-zero if the version has no section, so a release cannot ship without an entry. Run it
from the repository root:

    python3 .github/scripts/changelog-section.py 1.2.3
"""

import argparse
import re
import sys
from pathlib import Path


def section(changelog: str, version: str) -> str:
    """Return the body of the ``## [version]`` section of *changelog*, without its heading."""
    heading = re.compile(r"^## \[(?P<version>[^\]]+)\]", re.MULTILINE)
    headings = list(heading.finditer(changelog))
    for index, match in enumerate(headings):
        if match.group("version") != version:
            continue
        end = headings[index + 1].start() if index + 1 < len(headings) else len(changelog)
        body = changelog[match.end():end]
        # Drop the rest of the heading line (the date) and the trailing link references.
        body = body.split("\n", 1)[1] if "\n" in body else ""
        body = re.sub(r"^\[[^\]]+\]: \S+$", "", body, flags=re.MULTILINE)
        return body.strip() + "\n"
    raise SystemExit(f"CHANGELOG.md has no section for version {version}.")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("version", help="Version to look up, without a leading 'v'.")
    parser.add_argument(
        "--changelog", type=Path, default=Path("CHANGELOG.md"), help="Path to the changelog."
    )
    args = parser.parse_args()
    sys.stdout.write(section(args.changelog.read_text(encoding="utf-8"), args.version))


if __name__ == "__main__":
    main()
