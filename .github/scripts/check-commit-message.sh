#!/usr/bin/env bash
# Checks commit messages against the convention in CONTRIBUTING.md.
#
# Called two ways, so the hook and CI cannot drift apart:
#   check-commit-message.sh <file>          the message in a file, as Git hands it to a commit-msg hook
#   check-commit-message.sh --range <a>..<b> every commit in the range, as CI checks a push or a pull request
#
# Merge commits, reverts and the fixup!/squash! commits of an interactive rebase are accepted as Git writes them,
# and the " (#N)" that GitHub appends to a squash-merged pull request's title does not count towards the header length.
set -euo pipefail
shopt -s extglob

# Lengths count characters, so "é" counts as one, but the hook may start in the C locale, where they count bytes.
for locale in C.UTF-8 en_US.UTF-8; do
    { LC_ALL="$locale"; } 2>/dev/null
    probe=$'\xc3\xa9'
    if [ "${#probe}" -eq 1 ]; then
        break
    fi
done

types='feat|fix|docs|test|refactor|style|build|ci|chore'
scopes='auth|client|commit-check|contributing|dependabot|deps|diagnostics|docs|editorconfig|errors|files|github|options|package|pages|readme|release|revisions|rider|scripts|site|sln|smoke|tests'
header_pattern="^(${types})(\(([^()]+)\))?(!?): ([^ ].*[^. ])$"
scope_pattern="^(${scopes})$"
breaking_footer_pattern='^BREAKING CHANGE: [^ ]'
max_header_length=72
max_body_line_length=72
# Body lines that may run longer because they cannot be wrapped: one holding a URL, a trailer such as Co-authored-by, and the
# "* <header>" GitHub writes for each commit in the body of a squash merge, which was checked as a header on its own.
unwrappable_line_pattern="://|^[A-Za-z]+(-[A-Za-z]+)+: |^\* (${types})(\([^()]+\))?!?: "
fixes_footer_pattern='^Fixes #[0-9]+$'
# Any of GitHub's closing keywords before an issue here or in another repository, matched against the lowercased line.
closing_keyword_pattern='(^|[^a-z0-9_])(close[sd]?|fix(e[sd])?|resolve[sd]?):?[[:space:]]+([a-z0-9_.-]+/[a-z0-9_.-]+)?#[0-9]+'

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
        if [[ "$header" =~ $header_pattern ]]; then
            local scope="${BASH_REMATCH[3]}"
            local breaking="${BASH_REMATCH[4]}"
            local subject="${BASH_REMATCH[5]}"
            if [ -n "$scope" ] && ! [[ "$scope" =~ $scope_pattern ]]; then
                problems+=("the scope '${scope}' is not one of ${scopes//|/, }")
            fi
            if [[ "$subject" != [a-z]* ]]; then
                problems+=("the subject starts with '${subject%% *}' rather than a lowercase letter")
            fi
            local breaking_footer=''
            for line in "${kept[@]:1}"; do
                [[ "$line" =~ $breaking_footer_pattern ]] && breaking_footer='yes'
                if [[ "$line" == BREAKING-CHANGE:* ]]; then
                    problems+=("the footer is spelled 'BREAKING-CHANGE:'; write 'BREAKING CHANGE:' instead")
                fi
            done
            if [ -n "$breaking" ] && [ -z "$breaking_footer" ]; then
                problems+=("the '!' marks a breaking change, but no 'BREAKING CHANGE:' footer says what breaks")
            elif [ -z "$breaking" ] && [ -n "$breaking_footer" ]; then
                problems+=("the 'BREAKING CHANGE:' footer needs a '!' before the ':' in the header")
            fi
        else
            problems+=("the header does not match '<type>(<scope>)!: <subject>' with a type of ${types//|/, }, a non-empty subject and no trailing period")
        fi
        local measured="${header%% (#+([0-9]))}"
        if [ "${#measured}" -gt "$max_header_length" ]; then
            problems+=("the header is ${#measured} characters; the limit is ${max_header_length}")
        fi
    fi
    if [ "${#kept[@]}" -gt 1 ] && [ -n "${kept[1]}" ]; then
        problems+=('the header must be followed by a blank line')
    fi
    local number previous
    for ((number = 2; number <= ${#kept[@]}; number++)); do
        line="${kept[number - 1]}"
        previous="${kept[number - 2]}"
        if [ "${#line}" -gt "$max_body_line_length" ] && ! [[ "$line" =~ $unwrappable_line_pattern ]]; then
            problems+=("line ${number} is ${#line} characters; the limit for the body is ${max_body_line_length}")
        fi
        if [[ "$line" =~ $fixes_footer_pattern ]]; then
            if [ -n "$previous" ] && ! [[ "$previous" =~ $fixes_footer_pattern ]]; then
                problems+=("line ${number} needs a blank line before it, so that 'Fixes #N' starts a paragraph of its own")
            fi
        elif [[ "${line,,}" =~ $closing_keyword_pattern ]]; then
            problems+=("line ${number} closes an issue; only a line of its own reading 'Fixes #N' may do that")
        fi
    done

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
