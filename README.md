# Undertale Localization Project

Translations in the standard gettext PO format (`locale/<code>.po`, one
per language), compiled directly into the game's own built-in multi-language
system by `.csx` scripts run through `UndertaleModCli`. All languages ship
in a single `data.win`, selectable at runtime from Settings → Language.

## I want to play Undertale translated

Download `data.win` from the [**Multilingual** release](../../releases/tag/multilingual)
(install instructions for every language are on the release page).

| Language | Coverage | Credits |
| --- | --- | --- |
| 🇵🇱 Polski | 94.7% | Krzyhau |
| 🇪🇸 Español | 95.4% | [Undertale-Spanish (UTES) v1.1](https://undertale-spanish.com/) |

Coverage is against `locale/undertale.pot`, i.e. the text the game's own
translation system can reach — see "Known limitations" below for text it
can't.

## Architecture

- **Undertale (Steam) already ships English+Japanese via its own text
  lookup system**: `scr_gettext(text_id)` reads `global.text_data_en`
  (baseline) and, if `global.language != "en"`, overrides with
  `global.text_data_<lang>` when that key exists there — missing keys
  fall back to English automatically, so no translation needs 100%
  coverage. Those tables are populated by GML scripts
  (`gml_Script_textdata_en`, `_ja`, ...), each just thousands of
  `ds_map_add(global.text_data_en, "key", "text");` lines. We compile one
  such script per locale from its `.po` file, add its language code to
  `global.lang_list`. The rest of the plumbing this needs (Settings menu
  cycling in place of vanilla's plain EN⇄JA toggle, `scr_gettext`'s own
  language lookup, which vanilla hardcodes to only ever check `"ja"`) is
  locale-count-independent, so it's expressed as plain unified diffs in
  `patches/` and applied via the system `patch` binary rather than
  hardcoded in `scripts/build_data.csx` — see `patches/README.md`. See
  `scripts/lib/GmlText.csx` for how GML string literals are read/written,
  and its header comment for what's *not* built into the base game (the
  array-cycling menu, notably — that had to be added, not just extended).
- **The tooling is C#/.NET (`UndertaleModCli` + `UndertaleModLib`)**, both
  for that GML compilation (`UndertaleModLib.Compiler.CodeImportGroup`) and
  because `data.win`'s raw, absolute pointers to strings/assets need
  `UndertaleModLib`'s full re-serialization to stay consistent after any
  edit. Reimplementing that in plain Python would be a large, risky
  project.
- **All the logic lives in `.csx` scripts** run via `UndertaleModCli load
  --scripts` — not a separate compiled project. `.csx` files run through the
  built-in Roslyn scripting host in `UndertaleModCli`, so no .NET SDK or
  compilation step is needed.
- **Translations use the real `.po` format** (gettext), not ad-hoc JSON —
  editable in Poedit/Lokalize, with support for `#, fuzzy` (needs review)
  and comments.
- **Some UI text is baked into sprite pixels, not GML strings** (`FIGHT`,
  `ACT`, `ITEM`, `MERCY`, sign graphics, minigame images) — `scr_gettext`
  can't reach it, no matter what's in a `.po`. Vanilla already has an
  analogous mechanism for this, `scr_getsprite()`, hardcoded the same way
  `scr_gettext` was (`global.language == "ja"` swaps in a `..._ja` sprite
  variant); `patches/gml_Script_scr_getsprite.patch` generalizes it the
  same way, into a dynamic `asset_get_index(name + "_" + global.language)`
  lookup. See `sprites/README.md` — this needs actual translated artwork,
  not just a code change; `es` reuses artwork from Undertale-Spanish
  (UTES) v1.1, `pl` currently has none.

## Known limitations

Not every piece of in-game text goes through `scr_gettext` — some is
written directly into other objects' GML as a plain string literal (e.g.
the `LV`/`HP`/`G` stat labels, the name-picking screen, some dialogue).
That text isn't in `gml_Script_textdata_en`, so it's not in
`locale/undertale.pot` either, and no `.po` translation can currently
reach it — `scripts/validate_po.py` reports translated entries for such
text as "stale" (present in the `.po`, absent from the `.pot`) since
they don't correspond to any translatable key.

Some sign/UI graphics also can't be reached yet even with translated
artwork in hand: their vanilla object either doesn't exist at all (drawn
directly as a room tile - `spr_out_to_lunch_sign`) or would need a brand
new object placed in a room (`obj_grillbysign`, `obj_mtthotelsign`,
`obj_schoolsign`, `obj_temsign`, `obj_alphyslabsignl`/`r`, `obj_exitsign`,
`obj_inn_shopsign`, `obj_mtt_innershopsign`, `obj_mttshopsign` - all of
which UTES added from scratch). Room-editing is a category of change this
project hasn't attempted yet.

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
  lib/GmlText.csx           Reads/writes GML string literals as they appear in the game's
                            gml_Script_textdata_<lang> scripts (quote-style switching,
                            + concatenation - GML can't escape its own delimiter quote).
  lib/GmlPatch.csx          Applies a patches/*.patch unified diff to a decompiled GML string
                            via the system `patch` binary.
  lib/SpriteImport.csx      Imports sprites/*.png (translated UI graphics) as new sprite assets.
  extract_pot.csx           Decompiles gml_Script_textdata_en into a .pot (template) - every
                            real, in-game, user-facing string (not a raw Data.Strings dump).
  build_data.csx            Compiles every locale/<code>.po into the game's own multi-language
                            system + applies fonts/, sprites/, and patches/. See "Architecture" above.
  check_fonts.csx           Diagnostics: which characters are missing from which font.
  validate_po.py            Sanity-checks every locale/<code>.po against undertale.pot (dupes,
                            broken fuzzy entries, missing X-Display-Name header). Run manually
                            or via CI, see .github/workflows/.

locale/                   See locale/README.md.
  undertale.pot             Template: every translatable string in the current game, in
                            English, empty msgstr.
  <code>.po                 One file per language (e.g. pl.po, es.po). <code> is used directly
                            as the in-game language code - see "Adding a new language" below.

patches/                  Locale-count-independent GML changes (Settings menu cycling,
                          scr_gettext's language lookup), as unified diffs applied by
                          build_data.csx. See patches/README.md.

fonts/                    Font sheets (PNG + CSV) with an extended character set,
                          shared by ALL locales (not just pl).
                          See fonts/README.md.

sprites/                  Translated UI graphics (button/sign images with text baked into
                          the pixels - scr_gettext can't reach these). See sprites/README.md.

.github/workflows/        CI: validates every locale/*.po on every push/PR, and builds+publishes
                          a ready-to-use data.win to the "multilingual" GitHub Release whenever
                          a translation changes on main (see the top of this README).

build/                    Scratch space, gitignored EXCEPT `pristine.win` (tracked via Git LFS
                          — the only copy of a clean, original data.win; CI needs it to build
                          releases, and without it locally you'd have to make one again from a
                          not-yet-localized copy of the game). Everything else here (dumps,
                          backups, working `.win` files) can be cleared at any time.
```

## Workflow: adding/fixing a translation

1. Open `locale/<code>.po` (e.g. `locale/pl.po`) in Poedit / Lokalize / any
   text editor. Each entry is `msgid "English original"` / `msgstr
   "Translation"`. Entries marked `#, fuzzy` come from an automatic match
   against an older imported translation and haven't been individually
   verified against the current version of the game — worth reviewing and
   clearing the `fuzzy` flag once checked.

2. Looking for a specific piece of missing text? Grep `locale/undertale.pot`
   (always English, deduplicated, one entry per real in-game string) —
   regenerate it first if you suspect it's stale (see `locale/README.md`).

3. Add/edit the entry in `locale/<code>.po` (the exact content of `msgid`
   must match the original, including `&`, `#`, `\[1]` etc. — these are the
   game's own formatting markers, not real newlines).

4. Build and install (always on a copy, never overwrite the original
   without a backup). Keep backups in `build/`:
   ```bash
   # Always build from the CLEAN original, NOT from an already-patched file.
   # Make build/pristine.win ONCE, right after installing/updating the game, before
   # any localized data.win overwrites it — then keep that copy in build/
   # and use it instead of $GAME/data.win (which may already be localized).
   # cp "$GAME/data.win" build/pristine.win

   UndertaleModCli/UndertaleModCli load build/pristine.win -v \
     --scripts scripts/build_data.csx \
     -o build/data_multi.win -f

   cp "$GAME/data.win" "build/data_backup_$(date +%Y%m%d_%H%M%S).win"
   cp build/data_multi.win "$GAME/data.win"
   md5 "$GAME/data.win" build/data_multi.win   # must match
   ```
   `build_data.csx` bundles **every** `locale/*.po` with translated content
   into the one output file — in-game, pick the language from
   Settings → Language.

### Adding a new language

Create `locale/<code>.po` with an `X-Display-Name` header (the label shown
in the Settings menu — ALL CAPS to match the existing `ENGLISH`/`JAPANESE`
labels), e.g.:
```
msgid ""
msgstr ""
"Content-Type: text/plain; charset=UTF-8\n"
"X-Display-Name: FRANÇAIS\n"
```
`<code>` becomes the in-game language code directly (`global.language`,
`gml_Script_textdata_<code>`) — use the code the game itself would use
(2-letter, lowercase; e.g. `fr`, `de`). `scripts/build_data.csx` picks up
any `locale/*.po` automatically, no separate registration needed. Make sure
`fonts/` covers the language's characters (see `fonts/README.md`).

## `.po` format — conventions adopted in this project

- `msgid` = the exact English text from the current (latest) version of the
  game.
- Empty `msgstr` = untranslated; `scripts/build_data.csx` simply doesn't
  emit that key, and `scr_gettext`'s own runtime fallback to English
  handles it — no special-casing needed on our end.
- `#, fuzzy` = needs review (bulk-imported from an older translation, not
  verified against the current version of the game).
- The header must include `X-Display-Name`, ALL CAPS (the language's name
  as shown in the in-game Settings menu, matching `ENGLISH`/`JAPANESE`,
  e.g. `POLSKI`, `ESPAÑOL`) — see "Adding a new language" above.
  `scripts/validate_po.py` fails if it's missing.
- `msgid`/`msgstr` are deduplicated by content — if the same English text
  appears in multiple places in the game, it gets ONE shared translation.
  If two identical strings in different contexts ever need different
  translations, this format doesn't support that — it would require adding
  `msgctxt` (not yet supported by `PoFile.csx`).
- No real use of `.mo` (the compiled binary form) — nothing here consumes
  `.mo` at runtime, `build_data.csx` reads `.po` directly.
