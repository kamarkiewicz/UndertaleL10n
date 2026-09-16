# patches/

Locale-count-independent GML changes needed for the multi-language system
(Settings-menu cycling, `scr_gettext`'s language lookup) - the structural
plumbing that's the same regardless of which/how many `locale/*.po` files
exist. Applied by `scripts/build_data.csx` on every build, via the system
`patch` binary (present by default on macOS and Ubuntu, incl. GitHub
Actions' `ubuntu-latest` runner).

Per-locale content (compiling each `.po` into `gml_Script_textdata_<code>`,
and the `global.lang_list`/`script_execute` lines in `obj_time_Create_0`)
is inherently dynamic - it depends on which locales are bundled - so it
stays generated directly in `scripts/build_data.csx` rather than living
here.

## Convention

- One file per patched code entry: `<code-entry-name>.patch`, e.g.
  `gml_Script_scr_gettext.patch` patches the `gml_Script_scr_gettext`
  code entry. `scripts/build_data.csx` derives the target entry from the
  filename and fails loudly if no such entry exists in the loaded
  `data.win`.
- A plain unified diff (`diff -u old new`, or `git diff --no-index old
  new`) against that entry's **decompiled** GML (i.e. UndertaleModLib's
  decompiler output, not the original GameMaker source - see
  `scripts/lib/GmlText.csx`'s header for why literal formatting matters
  here).
- Applied with `patch --fuzz=0`, so it fails the build (rather than
  silently drifting) if the vanilla decompile no longer matches the
  patch's context lines - e.g. after a game update changes this code
  entry, or after a UndertaleModTool decompiler update changes its output
  formatting for unrelated reasons. Regenerate the patch against a fresh
  decompile in that case.

## Adding/editing a patch

1. Decompile the target entry from a clean `build/pristine.win` (e.g. via
   a throwaway `.csx` calling `GetDecompiledText`) to get the "before"
   text.
2. Copy it and make the edit you want, to get the "after" text.
3. `diff -u before.gml after.gml > patches/<code-entry-name>.patch`
   (the `---`/`+++` header lines' exact paths don't matter - only the
   hunk content does).
4. Rebuild (`scripts/build_data.csx`) and check the log line
   `[patches] Applied ...` for that file, then spot-check the result by
   decompiling the built `data.win`'s copy of that entry.
