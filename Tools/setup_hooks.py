#!/usr/bin/env python3
"""Turns on the repository's git hooks (.githooks/) for this clone: python Tools/setup_hooks.py

The hooks run Tools/check_project.py before every commit and every push, so merge-conflict markers, broken
braces, missing .meta files and the other repository errors are stopped on your machine instead of reaching
main. Opening the Unity project does the same thing automatically (Scripts/Editor/GitHooksInstaller.cs);
this script is for people who have not opened Unity yet, or want to check that it worked.
"""
import os
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))


def git(*args):
    return subprocess.run(['git', '-C', ROOT] + list(args), capture_output=True, text=True)


def main():
    if not os.path.isdir(os.path.join(ROOT, '.githooks')):
        print('ERROR: .githooks/ not found in %s' % ROOT)
        return 1
    result = git('config', 'core.hooksPath', '.githooks')
    if result.returncode != 0:
        print('ERROR: could not set core.hooksPath: %s' % result.stderr.strip())
        return 1
    # Hooks must be executable on macOS/Linux; harmless on Windows.
    for name in ('pre-commit', 'pre-push', 'run-checks.sh'):
        path = os.path.join(ROOT, '.githooks', name)
        if os.path.isfile(path):
            os.chmod(path, 0o755)
    print('Git hooks are on: Tools/check_project.py now runs before every commit and push.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
