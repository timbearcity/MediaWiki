#!/usr/bin/env python3
"""Gate a build on the Cobertura report produced by `dotnet test -- --coverage`.

Reads the line and branch rates from the report, prints them, and exits non-zero if either falls
below the minimum. Run it after the test step, from the repository root:

    python3 .github/scripts/check-coverage.py --minimum 100
"""

import argparse
import os
import re
import sys
import xml.etree.ElementTree as ElementTree
from pathlib import Path

SKIPPED_DIRECTORIES = {"bin", "obj", ".git"}

# The README badge is a handwritten shields.io URL, so it can only stay truthful if something
# compares it against the threshold this script enforces. --badge does that.
BADGE_PATTERN = re.compile(r"img\.shields\.io/badge/coverage-(\d+(?:\.\d+)?)%25-")


def find_report(root: Path, name: str) -> Path:
    """Return the most recently written report called *name* under *root*."""
    candidates = [
        path
        for path in root.rglob(name)
        if not SKIPPED_DIRECTORIES.intersection(path.parts)
    ]
    if not candidates:
        raise SystemExit(
            f"No coverage report named {name!r} was found under {root}. "
            "Did the test step run with --coverage?"
        )
    return max(candidates, key=lambda path: path.stat().st_mtime)


def read_rates(report: Path) -> tuple[float, float]:
    """Return the (line, branch) coverage of *report* as percentages."""
    root = ElementTree.parse(report).getroot()
    try:
        return float(root.attrib["line-rate"]) * 100, float(root.attrib["branch-rate"]) * 100
    except KeyError as error:
        raise SystemExit(f"{report} is missing the {error.args[0]!r} attribute.") from error


def check_badge(badge_file: Path, minimum: float) -> list[str]:
    """Return the reasons the badge in *badge_file* disagrees with *minimum*, if any."""
    if not badge_file.exists():
        return [f"{badge_file} does not exist, so its coverage badge could not be checked"]
    match = BADGE_PATTERN.search(badge_file.read_text(encoding="utf-8"))
    if match is None:
        return [f"{badge_file} has no shields.io coverage badge to check"]
    claimed = float(match.group(1))
    if claimed != minimum:
        return [
            f"The coverage badge in {badge_file} claims {claimed:g}%, but the enforced minimum is "
            f"{minimum:g}%. Update the badge or the threshold so they agree."
        ]
    return []


def write_summary(report: Path, line_rate: float, branch_rate: float, minimum: float) -> None:
    """Append a table to the GitHub Actions job summary, when one is available."""
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if not summary:
        return
    verdict = "pass" if min(line_rate, branch_rate) >= minimum else "fail"
    with open(summary, "a", encoding="utf-8") as handle:
        handle.write(
            f"## Coverage\n\n"
            f"| Metric | Covered | Minimum | Result |\n"
            f"| --- | ---: | ---: | --- |\n"
            f"| Line | {line_rate:.2f}% | {minimum:.2f}% | {verdict} |\n"
            f"| Branch | {branch_rate:.2f}% | {minimum:.2f}% | {verdict} |\n\n"
            f"Report: `{report.as_posix()}`\n\n"
        )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--minimum",
        type=float,
        default=100.0,
        help="Lowest acceptable percentage for both line and branch coverage (default: 100).",
    )
    parser.add_argument(
        "--report",
        type=Path,
        default=None,
        help="Path to the Cobertura report. Searched for under the working directory when omitted.",
    )
    parser.add_argument(
        "--name",
        default="coverage.cobertura.xml",
        help="File name to search for (default: coverage.cobertura.xml).",
    )
    parser.add_argument(
        "--badge",
        type=Path,
        default=None,
        help="File whose shields.io coverage badge has to agree with --minimum, usually README.md.",
    )
    arguments = parser.parse_args()

    report = arguments.report or find_report(Path.cwd(), arguments.name)
    line_rate, branch_rate = read_rates(report)

    print(f"Report: {report}")
    print(f"Line coverage:   {line_rate:.2f}%")
    print(f"Branch coverage: {branch_rate:.2f}%")
    print(f"Minimum:         {arguments.minimum:.2f}%")

    write_summary(report, line_rate, branch_rate, arguments.minimum)

    problems = [
        f"{label} coverage is {rate:.2f}%, below the required {arguments.minimum:.2f}%"
        for label, rate in (("Line", line_rate), ("Branch", branch_rate))
        if rate < arguments.minimum
    ]
    if arguments.badge is not None:
        problems += check_badge(arguments.badge, arguments.minimum)

    for problem in problems:
        print(f"::error::{problem}")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
