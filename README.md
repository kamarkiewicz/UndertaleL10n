# Undertale Localization Project

Translation in the standard gettext PO format (`locale/pl_PL.po`), applied
to `data.win` by `.csx` scripts run through `UndertaleModCli`.

## Chcę zagrać w Undertale po polsku

Pobierz `data.win` z [release'a **Tłumaczenie PL**](../../releases/tag/polish-release)
i podmień nim plik o tej samej nazwie w folderze gry.

## Architecture

- **The tooling is C#/.NET (`UndertaleModCli` + `UndertaleModLib`)**, because
  the `data.win` format uses raw, absolute pointers to strings/assets
  scattered across the whole file (bytecode, variable names, assets...).
  Safely changing the LENGTH of a string (and Polish translations are almost
  always longer than the English originals) requires fixing up all of those
  pointers — exactly what `UndertaleModLib` does during a full
  re-serialization of the file. Reimplementing that in plain Python would be
  a large, risky project.
- **All the logic lives in `.csx` scripts** run via `UndertaleModCli load
  --scripts` — not a separate compiled project. `.csx` files run through the
  built-in Roslyn scripting host in `UndertaleModCli`, so no .NET SDK or
  compilation step is needed.
- **Translations use the real `.po` format** (gettext), not ad-hoc JSON —
  editable in Poedit/Lokalize, with support for `#, fuzzy` (needs review)
  and comments.

## Setup

`UndertaleModCli/` is in `.gitignore` and not part of the repo. `build/` is
gitignored too, except `build/pristine.win`, which is tracked via Git LFS
(see the "Directory layout" section below).

1. Download the nightly `UndertaleModCli` build for your system:
   https://github.com/UnderminersTeam/UndertaleModTool/releases/tag/nightly

   Pick the zip matching your OS/architecture (e.g. on macOS Apple Silicon:
   the `osx-arm64` build).

2. Extract the zip's contents into a `UndertaleModCli/` directory at the
   root of this repo, so the executable ends up directly at:
   - macOS/Linux: `UndertaleModCli/UndertaleModCli`
   - Windows: `UndertaleModCli\UndertaleModCli.exe`

3. On macOS/Linux, make it executable:
   ```bash
   chmod +x UndertaleModCli/UndertaleModCli
   ```

4. Set the path to your installed copy of the game (specific to your
   machine):
   ```bash
   export GAME="/path/to/steamapps/common/Undertale"
   ```
   All the examples below assume `$GAME` is already set.

5. Create a working directory:
   ```bash
   mkdir -p build
   ```

## Directory layout

```
UndertaleModCli/          Downloaded UndertaleModCli build (see Setup). Not in git.

scripts/                  Our tooling.
  lib/PoFile.csx            Reader/writer for the .po format (msgid/msgstr, #, fuzzy, comments).
  extract_pot.csx           Dumps ALL unique strings from a given data.win into a .pot (template).
  apply_locale.csx          Applies locale/<code>.po + fonts/ (shared) onto the in-memory Data.
  check_fonts.csx           Diagnostics: which characters are missing from which font.
  validate_po.py            Sanity-checks pl_PL.po against undertale.pot (dupes, broken fuzzy
                            entries). Run manually or via CI, see .github/workflows/.
  extract_strg_emergency.py EXCEPTION to "pure C#" — see the section below for when to use it.

locale/                   See locale/README.md.
  undertale.pot             Template: all strings from the current game, in English, empty msgstr.
  pl_PL.po                  The ACTUAL Polish translation. This is what you edit.

fonts/                    Font sheets (PNG + CSV) with an extended character set,
                          shared by ALL locales (not just pl).
                          See fonts/README.md.

.github/workflows/        CI: validates pl_PL.po on every push/PR, and builds+publishes a
                          ready-to-use data.win to the "polish-release" GitHub Release whenever
                          the translation changes on main (see the top of this README).

build/                    Scratch space, gitignored EXCEPT `pristine.win` (tracked via Git LFS
                          — the only copy of a clean, original data.win; CI needs it to build
                          releases, and without it locally you'd have to make one again from a
                          not-yet-localized copy of the game). Everything else here (dumps,
                          backups, working `.win` files) can be cleared at any time.
```

## Workflow: adding/fixing a translation

1. Open `locale/pl_PL.po` in Poedit / Lokalize / any text editor. Each entry
   is `msgid "English original"` / `msgstr "Polish translation"`. Entries
   marked `#, fuzzy` come from an automatic match against Krzyhau's old
   2017 translation and haven't been individually verified against the
   current version of the game — worth reviewing and clearing the `fuzzy`
   flag once checked.

2. Looking for a specific piece of missing text from the game? Generate a
   fresh `.pot` from the currently installed game and grep it:
   ```bash
   cp "$GAME/data.win" build/current.win   # local copy - a slow/networked FS can cause issues
   UndertaleModCli/UndertaleModCli dump build/current.win -s -o build/dump
   grep -n -i "text you're looking for" build/dump/strings.txt
   ```
   Note: if the currently installed game already has some translations from
   `pl_PL.po` applied, the dump will show POLISH text for those, not
   English — search `locale/undertale.pot` instead (always English) if
   you're not sure whether a given string already has a PO entry.

3. Add/edit the entry in `locale/pl_PL.po` (the exact content of `msgid`
   must match the original, including `&`, `#`, `\[1]` etc. — these are the
   game's own formatting markers, not real newlines).

4. Build and install (always on a copy, never overwrite the original
   without a backup). Keep backups in `build/`:
   ```bash
   # Always build from the CLEAN original, NOT from an already-patched file —
   # apply_locale.csx is not idempotent with respect to already-replaced strings
   # (it won't reconstruct the original English text to re-match against the PO).
   # Make build/pristine.win ONCE, right after installing/updating the game, before
   # any localized data.win overwrites it — then keep that copy in build/
   # and use it instead of $GAME/data.win (which may already be localized).
   # cp "$GAME/data.win" build/pristine.win

   UTLOC_LOCALE=pl_PL UndertaleModCli/UndertaleModCli load build/pristine.win -v \
     --scripts scripts/apply_locale.csx \
     -o build/data_pl.win -f

   cp "$GAME/data.win" "build/data_PL_backup_$(date +%Y%m%d_%H%M%S).win"
   cp build/data_pl.win "$GAME/data.win"
   md5 "$GAME/data.win" build/data_pl.win   # must match
   ```

## When to use `extract_strg_emergency.py` (the only exception to pure C#)

Some old `data.win` files (e.g. very early GMS1.x versions) **fail to load
fully** through `UndertaleModCli`/`UndertaleModLib` — they throw an
exception while reading the FONT chunk (an old format incompatible with the
current parser). This script works around the problem by reading the IFF
container manually (tag+length+skip, skipping FONT without trying to
understand it) and parsing the STRG chunk directly from raw bytes. The
normal workflow (current game + `apply_locale.csx`) doesn't use it — it's
kept around in case a similar problem shows up with another old file in the
future.

Non-obvious findings from this work, useful for a similar problem:
- A pointer in the STRG table points AT the length field, not 4 bytes past
  it.
- In files with strings injected by tools that compute length before
  converting to UTF-8, the declared length can be wrong — the true end of
  the string is the NUL byte, not the declared length.

## `.po` format — conventions adopted in this project

- `msgid` = the exact English text from the current (latest) version of the
  game.
- Empty `msgstr` = untranslated; `apply_locale.csx` skips these (the
  English stays, a safe fallback instead of breaking something).
- `#, fuzzy` = needs review (bulk-imported from Krzyhau's old translation,
  not verified against the current version of the game).
- `msgid`/`msgstr` are deduplicated by content — if the same English text
  appears in multiple places in the game, it gets ONE shared translation.
  If two identical strings in different contexts ever need different
  translations, this format doesn't support that — it would require adding
  `msgctxt` (not yet supported by `PoFile.csx`).
- No real use of `.mo` (the compiled binary form) — nothing here consumes
  `.mo` at runtime, `apply_locale.csx` reads `.po` directly.
