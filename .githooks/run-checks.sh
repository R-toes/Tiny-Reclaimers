#!/bin/sh
# Shared by pre-commit and pre-push: runs Tools/check_project.py and stops the git command if it reports an error.

run_repository_checks() {
    root=$(git rev-parse --show-toplevel) || exit 1

    python_cmd=
    for candidate in python3 python py; do
        if command -v "$candidate" >/dev/null 2>&1 && "$candidate" -c "import sys; sys.exit(0 if sys.version_info[0] >= 3 else 1)" >/dev/null 2>&1; then
            python_cmd=$candidate
            break
        fi
    done

    if [ -z "$python_cmd" ]; then
        echo "Cannot run the repository checks: Python 3 was not found." >&2
        echo "Install Python 3 (python.org) so that 'python --version' works, then try again." >&2
        exit 1
    fi

    if ! "$python_cmd" "$root/Tools/check_project.py"; then
        echo "" >&2
        echo "Repository checks failed, so the $1 was stopped. Fix the errors listed above and try again." >&2
        echo "(Merge-conflict markers mean a merge or stash was left half-resolved: search for <<<<<<< and resolve it.)" >&2
        exit 1
    fi
}
