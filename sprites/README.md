# sprites/

Translated UI graphics - button/sign images with text baked into the
pixels (`FIGHT`, `ACT`, `ITEM`, `MERCY`, `SPARE`, `TALK`, sign text, minigame
graphics like the word-search and crossword). `scr_gettext` can't reach
this text since it's part of the image, not a GML string - see
`patches/gml_Script_scr_getsprite.patch`, which generalizes vanilla's
hardcoded `global.language == "ja"` sprite-swap table into a dynamic
`asset_get_index(base_name + "_" + global.language)` lookup, exactly like
`scr_gettext` does for text. `scripts/build_data.csx` imports every sprite
listed here as a new asset named exactly `<base_name>_<code>` (e.g.
`spr_fightbt_es`), so it's found automatically once that patch is applied
- no per-sprite registration needed.

This only covers sprites that a vanilla object already runs through
`scr_getsprite()` (or, for `spr_coresign`, that `patches/gml_Object_obj_coresign_Create_0.patch`
adds that call to) - i.e. sprites belonging to an object already placed in
a room. Sign graphics whose vanilla object doesn't exist at all (drawn
directly as a room tile, or requiring a brand new object placed in a
room) aren't covered by this mechanism and would need room-editing work
this project hasn't attempted yet.

## Format

- `sprites.csv`: one row per sprite - `name,width,height,origin_x,origin_y,
  margin_left,margin_right,margin_top,margin_bottom,bbox_mode,frame_count`.
- `frames.csv`: one row per frame - `name,frame_index,png_file,
  source_width,source_height,target_x,target_y,target_width,target_height,
  bounding_width,bounding_height` (the original texture-page packing
  values, reproduced exactly so trimmed sprites keep their alignment).
- `<name>_<frame>.png`: the actual frame image, exported at
  `source_width`x`source_height`.

Collision masks are seeded as a simple fully-solid rectangle on import -
these are UI/decorative sprites (buttons, signs), never used in
`place_meeting`-style pixel collision, so exact mask data doesn't matter,
only that valid mask data exists (the file format requires it).

## Current coverage

`es` (Español): 26 of the ~32 sprites vanilla's own sprite-swap table
supports, extracted from Undertale-Spanish (UTES) v1.1's own build (see
`locale/es.po`'s `X-Source` header and the "Architecture" section of the
main README) - `spr_barktry`, `spr_cbone`, `spr_dbone`, `spr_oolbone`,
`spr_snowsans`, and `spr_udebone` weren't translated by that patch either
and are simply absent here too.

`it` (Italiano): 64 sprites (the broadest coverage of any bundled
language, including the ones `es` is missing above), extracted from the
Undertale Spaghetti Project (USP) installer's own asset bundle (see
`locale/it.po`'s `X-Source` header). A couple of USP's sprites
(`spr_pressz`, `spr_wordtest_full`) don't correspond to a vanilla sprite
name in this game version and were skipped; two others
(`spr_fileerased_parts`, `spr_happybreaktime`) only had translated art for
some of their frames, so only those frames were imported.

`de` (Deutsch), `pl` (Polski): none - no source material exists to
extract from yet. These sprites fall back to English until someone
creates matching artwork (same `<base_name>_<code>.png` + a
`sprites.csv`/`frames.csv` entry would be picked up automatically - no
code changes needed).

## Adding a sprite

1. Get (or draw) the translated image(s), one PNG per frame, at the
   sprite's native (untrimmed source) resolution.
2. Add a row to `sprites.csv` and one row per frame to `frames.csv`. If
   you don't know the exact margin/bbox/target values, copying the
   corresponding vanilla sprite's own values (dump via a throwaway `.csx`
   calling into `Data.Sprites`) is a safe default for a non-trimmed,
   full-canvas image.
3. Rebuild (`scripts/build_data.csx`) and check the `[sprites] Imported
   N translated sprite(s).` log line, then confirm in-game (the sprite
   only takes effect once `patches/gml_Script_scr_getsprite.patch` is
   applied, which happens automatically whenever any locale is bundled).
