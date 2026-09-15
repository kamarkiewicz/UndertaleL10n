#!/usr/bin/env python3
"""Validates every locale/<code>.po against locale/undertale.pot.

Checks, per .po:
  - duplicate msgids within either file (breaks PoFile.csx's dedup lookup)
  - entries flagged #, fuzzy with an empty msgstr (nothing to review)
  - msgids present in the .po but absent from the current undertale.pot
    (stale/orphaned - warning only, since the .pot may simply predate a
    newer game build)

Exits non-zero (failing CI) only for the first two, which are concrete bugs.
"""

import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
LOCALE_DIR = REPO_ROOT / "locale"
POT_PATH = LOCALE_DIR / "undertale.pot"


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


def get_header(path):
    """Returns the header key/value dict (the msgstr of the empty-msgid entry)."""
    header = {}
    in_header_msgstr = False
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n\r").rstrip()
            if line == 'msgid ""':
                continue
            if line == 'msgstr ""':
                in_header_msgstr = True
                continue
            if in_header_msgstr and line.startswith('"'):
                raw = extract_quoted(line) or ""
                if ":" in raw:
                    key, _, value = raw.partition(":")
                    header[key.strip()] = value.strip()
                continue
            break
    return header


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


def validate_one(po_path, pot_entries, pot_dupes):
    po_entries = parse_po(po_path)

    pot_ids = {mid for mid, _, _, _ in pot_entries}
    po_ids = {mid for mid, _, _, _ in po_entries}

    po_dupes = find_duplicates(po_entries)
    fuzzy_empty = [e for e in po_entries if e[2] and e[1].strip() == ""]
    stale = po_ids - pot_ids
    translated = [e for e in po_entries if e[1].strip() != ""]

    print(f"\n=== {po_path.name} ===")
    print(f"po entries (unique msgid):  {len(po_ids)}")
    print(
        f"translated: {len(translated)} "
        f"({100 * len(translated) / max(len(po_ids), 1):.1f}% of po entries, "
        f"{100 * len(translated) / max(len(pot_ids), 1):.1f}% of the game)"
    )
    print(f"stale msgids (in po, not in current pot): {len(stale)}  (warning only)")

    ok = True

    display_name = get_header(po_path).get("X-Display-Name")
    if not display_name:
        ok = False
        print(f"\nFAIL: {po_path.name} is missing the 'X-Display-Name' header "
              f"(used as the in-game Settings menu label by scripts/build_data.csx).")
    else:
        print(f"X-Display-Name: {display_name}")

    if po_dupes:
        ok = False
        print(f"\nFAIL: {len(po_dupes)} duplicate msgid(s) in {po_path.name}:")
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

    return ok


def main():
    if not POT_PATH.exists():
        print(f"ERROR: {POT_PATH} not found (run scripts/extract_pot.csx).")
        return 1

    po_paths = sorted(LOCALE_DIR.glob("*.po"))
    if not po_paths:
        print(f"ERROR: no locale/*.po files found in {LOCALE_DIR}.")
        return 1

    pot_entries = parse_po(POT_PATH)
    pot_dupes = find_duplicates(pot_entries)
    print(f"pot entries (unique msgid): {len({mid for mid, _, _, _ in pot_entries})}")

    ok = True
    for po_path in po_paths:
        if not validate_one(po_path, pot_entries, pot_dupes):
            ok = False

    print("\n" + ("PASS" if ok else "FAIL"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
