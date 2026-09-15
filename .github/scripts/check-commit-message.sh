#!/usr/bin/env bash
# Checks commit messages against the convention in CONTRIBUTING.md.
#
# Called two ways, so the hook and CI cannot drift apart:
#   check-commit-message.sh <file>          the message in a file, as Git hands it to a commit-msg hook
#   check-commit-message.sh --range <a>..<b> every commit in the range, as CI checks a push or a pull request
#
# Merge commits, reverts and the fixup!/squash! commits of an interactive rebase are accepted as Git writes them.
set -euo pipefail

types='feat|fix|docs|test|refactor|style|build|ci|chore'
header_pattern="^(${types})(\([a-z0-9._-]+\))?!?: [^ ].*[^. ]$"
max_header_length=72

# Prints the problems with one message, read from stdin, and fails when there are any.
check_message() {
    local -a lines
    mapfile -t lines
    # Comment lines are what Git strips before committing, so they do not count.
    local -a kept=()
    local line
    for line in "${lines[@]}"; do
        [[ "$line" == \#* ]] || kept+=("$line")
    done
    local header="${kept[0]:-}"
    local -a problems=()

    case "$header" in
        'Merge '*|'Revert "'*|'fixup! '*|'squash! '*) return 0 ;;
    esac

    if [ -z "$header" ]; then
        problems+=('the message is empty')
    else
        if ! [[ "$header" =~ $header_pattern ]]; then
            problems+=("the header does not match '<type>(<scope>)!: <subject>' with a type of ${types//|/, }, a non-empty subject and no trailing period")
        fi
        if [ "${#header}" -gt "$max_header_length" ]; then
            problems+=("the header is ${#header} characters; the limit is ${max_header_length}")
        fi
    fi
    if [ "${#kept[@]}" -gt 1 ] && [ -n "${kept[1]}" ]; then
        problems+=('the header must be followed by a blank line')
    fi

    if [ "${#problems[@]}" -eq 0 ]; then
        return 0
    fi
    printf '%s\n' "  ${header}"
    printf '  - %s\n' "${problems[@]}"
    return 1
}

if [ "${1:-}" = '--range' ]; then
    range="${2:?a range such as a1b2c3..d4e5f6 is required}"
    failed=0
    for sha in $(git rev-list --no-merges "$range"); do
        if ! git log -1 --format=%B "$sha" | check_message; then
            echo "  in commit ${sha}" >&2
            failed=1
        fi
    done
    if [ "$failed" -ne 0 ]; then
        echo 'One or more commit messages do not follow CONTRIBUTING.md.' >&2
        exit 1
    fi
else
    file="${1:?the path of the commit message file is required}"
    if ! check_message < "$file"; then
        echo 'The commit message does not follow CONTRIBUTING.md; see the "Commit messages" section there.' >&2
        exit 1
    fi
fi
