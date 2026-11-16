#!/usr/bin/env python3
"""Repository checks that need no Unity install (run by CI, and by hand: python Tools/check_project.py).

Errors (exit code 1):
  - an asset or folder under Assets/ without its .meta file, or a .meta whose asset is gone
  - two .meta files with the same guid
  - a C# file with unbalanced braces (a quick way to catch a bad merge or edit before Unity compiles)
  - merge-conflict markers left in a text file
  - a C# file over MAX_LINES lines
Warnings (printed, exit code unchanged):
  - a C# file over WARN_LINES lines
  - a runtime script comparing against a stage id string such as "STAGE_04" (stage behaviour is StageData data;
    see PROJECT_GUIDELINES.md, "Stage environments")
"""
import os
import re
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
UNITY = os.path.join(ROOT, 'TRASH-TD')
ASSETS = os.path.join(UNITY, 'Assets')
SCRIPTS = os.path.join(ASSETS, '_Project', 'Scripts')

WARN_LINES = 1000
MAX_LINES = 1500
TEXT_EXTENSIONS = ('.cs', '.asmdef', '.md', '.json', '.shader', '.asset', '.prefab', '.unity', '.meta', '.yml', '.py')
# Files that may still name a stage id (default sandbox map, a legacy fallback). Keep this list short.
STAGE_ID_ALLOWED = ('StageBootstrapper.cs', 'MainMenuController.StageMap.cs', 'MainMenuController.cs',
                    'MainMenuController.StageDetails.cs', 'MainMenuController.TitleScreen.cs', 'StageProgress.cs',
                    'EnemyIntelProgress.cs', 'SaveSystem.cs', 'TutorialDirector.cs', 'AudioManager.cs')

errors = []
warnings = []


def rel(path):
    return os.path.relpath(path, ROOT).replace('\\', '/')


def tracked_or_all(base):
    """Files under base that git tracks or would track (not ignored); falls back to walking the folder."""
    try:
        out = subprocess.check_output(['git', 'ls-files', '--cached', '--others', '--exclude-standard', '-z', base],
                                      cwd=ROOT, stderr=subprocess.DEVNULL).decode('utf-8', 'replace')
        files = [os.path.join(ROOT, p) for p in out.split('\0') if p]
        if files:
            return [f for f in files if os.path.exists(f)]
    except (OSError, subprocess.CalledProcessError):
        pass
    result = []
    for d, _, names in os.walk(base):
        for n in names:
            result.append(os.path.join(d, n))
    return result


def hidden_to_unity(name):
    return name.startswith('.') or name.endswith('~') or name in ('desktop.ini',)


def check_metas(files):
    """Every asset/folder has a .meta and every .meta has its asset."""
    file_set = set(os.path.normpath(f) for f in files)
    dirs = set()
    for f in file_set:
        d = os.path.dirname(f)
        while len(d) > len(ASSETS) and d.startswith(ASSETS):
            dirs.add(d)
            d = os.path.dirname(d)

    def ignored(path):
        p = os.path.relpath(path, ASSETS)
        return any(hidden_to_unity(part) for part in p.split(os.sep))

    guids = {}
    for f in sorted(file_set):
        if not f.startswith(ASSETS) or ignored(f):
            continue
        if f.endswith('.meta'):
            target = f[:-5]
            if target not in file_set and target not in dirs:
                errors.append('orphan .meta (its asset is missing): ' + rel(f))
            try:
                text = open(f, encoding='utf-8', errors='replace').read()
            except OSError:
                continue
            m = re.search(r'^guid:\s*([0-9a-fA-F]{32})', text, re.M)
            if not m:
                errors.append('.meta without a guid: ' + rel(f))
            else:
                guids.setdefault(m.group(1).lower(), []).append(f)
        elif f + '.meta' not in file_set:
            errors.append('asset without a .meta: ' + rel(f))
    for d in sorted(dirs):
        if not ignored(d) and d + '.meta' not in file_set:
            errors.append('folder without a .meta: ' + rel(d))
    for guid, paths in guids.items():
        if len(paths) > 1:
            errors.append('guid %s used by %d files: %s' % (guid, len(paths), ', '.join(rel(p) for p in paths)))


def brace_balance(text):
    """Net brace depth of C# text, skipping comments, strings and chars. (Same lexer idea as the splitting tool.)"""
    n = len(text)
    depth = 0
    i = 0

    def skip_string(j):
        interp = verbatim = False
        while text[j] in '$@':
            if text[j] == '$':
                interp = True
            else:
                verbatim = True
            j += 1
        j += 1  # opening quote
        while j < n:
            c = text[j]
            if verbatim:
                if c == '"':
                    if text[j + 1:j + 2] == '"':
                        j += 2
                        continue
                    return j + 1
            else:
                if c == '\\':
                    j += 2
                    continue
                if c == '"':
                    return j + 1
                if c == '\n':
                    return j
            if interp and c == '{':
                if text[j + 1:j + 2] == '{':
                    j += 2
                    continue
                j = skip_hole(j + 1)
                continue
            if interp and c == '}' and text[j + 1:j + 2] == '}':
                j += 2
                continue
            j += 1
        return j

    def skip_hole(j):
        d = 0
        while j < n:
            c = text[j]
            if c == '"' or (c in '$@' and text[j + 1:j + 2] in ('"', '$', '@')):
                j = skip_string(j)
                continue
            if c == "'":
                j = skip_char(j)
                continue
            if c == '{':
                d += 1
            elif c == '}':
                if d == 0:
                    return j + 1
                d -= 1
            j += 1
        return j

    def skip_char(j):
        if text[j + 1:j + 2] == '\\':
            k = text.find("'", j + 3)
            return (k if k >= 0 else j + 2) + 1
        return j + 3

    while i < n:
        c = text[i]
        if c == '/' and text[i + 1:i + 2] == '/':
            while i < n and text[i] != '\n':
                i += 1
        elif c == '/' and text[i + 1:i + 2] == '*':
            end = text.find('*/', i + 2)
            i = n if end < 0 else end + 2
        elif c == '"' or (c in '$@' and text[i + 1:i + 2] in ('"', '$', '@')):
            i = skip_string(i)
        elif c == "'":
            i = skip_char(i)
        else:
            if c == '{':
                depth += 1
            elif c == '}':
                depth -= 1
            i += 1
    return depth


def check_text_files(files):
    for f in files:
        if not f.endswith(TEXT_EXTENSIONS) or not os.path.isfile(f):
            continue
        try:
            data = open(f, 'rb').read()
        except OSError:
            continue
        if b'\0' in data[:4096]:
            continue
        text = data.decode('utf-8-sig', 'replace')

        for m in re.finditer(r'^(<<<<<<< |>>>>>>> |=======$)', text, re.M):
            if f.endswith('.md') and m.group(1) == '=======':
                continue  # a setext heading underline
            errors.append('merge-conflict marker in %s (line %d)' % (rel(f), text.count('\n', 0, m.start()) + 1))
            break

        if not f.endswith('.cs'):
            continue
        lines = text.count('\n') + 1
        if lines > MAX_LINES:
            errors.append('%s has %d lines (max %d): split it by topic (see "Code layout" in PROJECT_GUIDELINES.md)' % (rel(f), lines, MAX_LINES))
        elif lines > WARN_LINES:
            warnings.append('%s has %d lines (aim for under %d)' % (rel(f), lines, WARN_LINES))

        try:
            balance = brace_balance(text.replace('\r\n', '\n'))
        except Exception as ex:  # a lexer bug must not hide the other checks
            warnings.append('could not check braces in %s: %s' % (rel(f), ex))
            balance = 0
        if balance != 0:
            errors.append('unbalanced braces in %s (net %+d)' % (rel(f), balance))

        in_runtime = f.startswith(SCRIPTS) and os.sep + 'Editor' + os.sep not in f
        if in_runtime and os.path.basename(f) not in STAGE_ID_ALLOWED:
            for m in re.finditer(r'(==|!=)\s*"STAGE_\d+"|case\s+"STAGE_\d+"', text):
                warnings.append('%s compares against a stage id string (line %d): use StageData data instead'
                                % (rel(f), text.count('\n', 0, m.start()) + 1))
                break


def main():
    if not os.path.isdir(ASSETS):
        print('Could not find %s' % ASSETS)
        return 2
    files = tracked_or_all(ASSETS)
    check_metas(files)
    check_text_files(files + [os.path.join(ROOT, 'PROJECT_GUIDELINES.md'), os.path.join(ROOT, 'README.md')])

    for w in warnings:
        print('warning: ' + w)
    for e in errors:
        print('ERROR: ' + e)
    print('%d files checked, %d error(s), %d warning(s)' % (len(files), len(errors), len(warnings)))
    return 1 if errors else 0


if __name__ == '__main__':
    sys.exit(main())
