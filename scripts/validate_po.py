#!/usr/bin/env python3
"""Validates locale/pl_PL.po against locale/undertale.pot.

Checks:
  - duplicate msgids within either file (breaks PoFile.csx's dedup lookup)
  - entries flagged #, fuzzy with an empty msgstr (nothing to review)
  - msgids present in pl_PL.po but absent from the current undertale.pot
    (stale/orphaned - warning only, since the .pot may simply predate a
    newer game build)

Exits non-zero (failing CI) only for the first two, which are concrete bugs.
"""

import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
POT_PATH = REPO_ROOT / "locale" / "undertale.pot"
PO_PATH = REPO_ROOT / "locale" / "pl_PL.po"


def unescape(s):
    out = []
    i = 0
    while i < len(s):
        c = s[i]
        if c == "\\" and i + 1 < len(s):
            n = s[i + 1]
            if n == "n":
                out.append("\n")
            elif n == "t":
                out.append("\t")
            elif n == "r":
                out.append("\r")
            elif n == '"':
                out.append('"')
            elif n == "\\":
                out.append("\\")
            else:
                out.append("\\")
                out.append(n)
            i += 2
        else:
            out.append(c)
            i += 1
    return "".join(out)


def extract_quoted(line):
    first = line.find('"')
    last = line.rfind('"')
    if first < 0 or last <= first:
        return None
    return unescape(line[first + 1 : last])


def parse_po(path):
    """Returns a list of (msgid, msgstr, fuzzy, line_number)."""
    entries = []
    msgid_buf = msgstr_buf = None
    in_msgid = in_msgstr = fuzzy = False
    start_line = None

    def flush():
        nonlocal msgid_buf, msgstr_buf, in_msgid, in_msgstr, fuzzy, start_line
        if msgid_buf is not None:
            mid = "".join(msgid_buf)
            mstr = "".join(msgstr_buf) if msgstr_buf is not None else ""
            if mid != "":  # skip the header entry (empty msgid)
                entries.append((mid, mstr, fuzzy, start_line))
        msgid_buf = msgstr_buf = None
        in_msgid = in_msgstr = fuzzy = False
        start_line = None

    with open(path, encoding="utf-8") as f:
        for lineno, raw in enumerate(f, 1):
            line = raw.rstrip("\n\r").rstrip()
            if line == "":
                flush()
                continue
            if line.startswith("#,"):
                if "fuzzy" in line:
                    fuzzy = True
                continue
            if line.startswith("#"):
                continue
            if line.startswith("msgid "):
                flush()
                msgid_buf = [extract_quoted(line) or ""]
                in_msgid, in_msgstr = True, False
                start_line = lineno
                continue
            if line.startswith("msgstr "):
                msgstr_buf = [extract_quoted(line) or ""]
                in_msgid, in_msgstr = False, True
                continue
            if line.startswith('"'):
                cont = extract_quoted(line) or ""
                if in_msgstr:
                    msgstr_buf.append(cont)
                elif in_msgid:
                    msgid_buf.append(cont)
                continue
        flush()
    return entries


def find_duplicates(entries):
    seen = {}
    for mid, _, _, _ in entries:
        seen[mid] = seen.get(mid, 0) + 1
    return {k: v for k, v in seen.items() if v > 1}


def main():
    if not POT_PATH.exists():
        print(f"ERROR: {POT_PATH} not found (run scripts/extract_pot.csx).")
        return 1
    if not PO_PATH.exists():
        print(f"ERROR: {PO_PATH} not found.")
        return 1

    pot_entries = parse_po(POT_PATH)
    po_entries = parse_po(PO_PATH)

    pot_ids = {mid for mid, _, _, _ in pot_entries}
    po_ids = {mid for mid, _, _, _ in po_entries}

    po_dupes = find_duplicates(po_entries)
    pot_dupes = find_duplicates(pot_entries)
    fuzzy_empty = [e for e in po_entries if e[2] and e[1].strip() == ""]
    stale = po_ids - pot_ids
    translated = [e for e in po_entries if e[1].strip() != ""]

    print(f"pot entries (unique msgid): {len(pot_ids)}")
    print(f"po entries (unique msgid):  {len(po_ids)}")
    print(
        f"translated: {len(translated)} "
        f"({100 * len(translated) / max(len(po_ids), 1):.1f}% of po entries, "
        f"{100 * len(translated) / max(len(pot_ids), 1):.1f}% of the game)"
    )
    print(f"stale msgids (in po, not in current pot): {len(stale)}  (warning only)")

    ok = True

    if po_dupes:
        ok = False
        print(f"\nFAIL: {len(po_dupes)} duplicate msgid(s) in {PO_PATH.name}:")
        for mid in list(po_dupes)[:20]:
            print(f"  {mid!r}")

    if pot_dupes:
        ok = False
        print(f"\nFAIL: {len(pot_dupes)} duplicate msgid(s) in {POT_PATH.name}:")
        for mid in list(pot_dupes)[:20]:
            print(f"  {mid!r}")

    if fuzzy_empty:
        ok = False
        print(f"\nFAIL: {len(fuzzy_empty)} entries marked #, fuzzy with an empty msgstr:")
        for mid, _, _, line in fuzzy_empty[:20]:
            print(f"  line {line}: {mid!r}")

    if stale:
        print(f"\nWARNING: {len(stale)} msgid(s) in po but not in the current .pot (sample):")
        for mid in list(stale)[:10]:
            print(f"  {mid!r}")

    print("\n" + ("PASS" if ok else "FAIL"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
