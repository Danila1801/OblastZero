#!/bin/bash
# Installs the OblastZero pre-commit hook.
#
#   bash tools/install_hooks.sh
#
# Copies rather than symlinks: a symlink into the working tree means a branch that
# does not carry tools/pre-commit silently disables the hook, and on Windows a
# symlink needs privileges this repo should not assume. The cost is that the hook
# must be reinstalled when tools/pre-commit changes, which the version check below
# reports on every run.

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_hook="${repo_root}/tools/pre-commit"
hooks_dir="$(git -C "${repo_root}" rev-parse --git-path hooks)"
target_hook="${hooks_dir}/pre-commit"

if [ ! -f "${source_hook}" ]; then
    echo "error: ${source_hook} not found." >&2
    exit 1
fi

mkdir -p "${hooks_dir}"

if [ -f "${target_hook}" ] && ! cmp -s "${source_hook}" "${target_hook}"; then
    backup="${target_hook}.replaced-$(date +%Y%m%d%H%M%S)"
    cp "${target_hook}" "${backup}"
    echo "note: an existing pre-commit hook was backed up to ${backup}"
fi

cp "${source_hook}" "${target_hook}"
chmod +x "${target_hook}"

echo "Installed pre-commit hook -> ${target_hook}"
echo
echo "It runs the fast gates only (content QA, C# string QA, localization QA), each"
echo "preceded by its own self-test. The compile and drift checks run in CI on push."
echo "Bypass a single commit with: git commit --no-verify"
