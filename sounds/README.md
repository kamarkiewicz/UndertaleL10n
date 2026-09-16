# sounds/

Translated embedded sound effects - voice lines/jokes that don't work as
plain text (e.g. Flowey's "that's a wonderful idea!" line, which plays a
distinct audio clip rather than going through `scr_gettext`).

Only covers **embedded** sounds (baked into `data.win`'s own SOND/AUDO
chunks). Undertale also streams some audio - notably background music -
from `.ogg` files that live next to `data.win` in the game folder, not
inside it; `scripts/build_data.csx` only ever produces a `data.win`, so
it can't distribute or select those external files. That category isn't
covered here (see README.md's "Known limitations").

## Format

`<base_name>_<code>.wav`, e.g. `snd_wonderfulidea_it.wav`. On import,
`scripts/lib/SoundImport.csx` clones the base sound's own metadata
(volume, pitch, effects, audio group, embedding flags) from the sound
named `<base_name>` and only swaps in the new audio bytes - so a
translated `.wav` should match the original's format/sample rate
reasonably closely to sound right.

For this to actually play at runtime, the one GML call site that plays
`<base_name>` needs a matching patch generalizing whatever hardcoded
language check it has (vanilla only ever wires up `_ja`, the same
situation `scr_gettext`/`scr_getsprite` were in - see `patches/README.md`).
There's no single shared "get sound for language" function to generalize
once the way there is for sprites (`scr_getsprite`), so each sound needs
its own small patch at its own call site.

## Current coverage

`it` (Italiano): `snd_wonderfulidea_it.wav`, extracted from the Undertale
Spaghetti Project (USP) installer's own asset bundle (see `locale/it.po`'s
`X-Source` header) - paired with
`patches/gml_Object_obj_floweytrigger2_Step_0.patch`, which generalizes
that one call site's `global.language == "ja"` check into a dynamic
`asset_get_index("snd_wonderfulidea_" + global.language)` lookup.

No other language has a translated version of this sound yet - it plays
in English for them.
