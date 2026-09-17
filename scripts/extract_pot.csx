// Extracts the game's translatable text into a .pot template (empty
// msgstr), sourced from gml_Script_textdata_en - the GML script that
// populates global.text_data_en, the lookup table scr_gettext() reads at
// runtime (see scripts/lib/GmlText.csx for how this system works). This is
// every real, in-game, user-facing string - unlike a raw Data.Strings dump,
// it doesn't include function/variable/asset names and other non-dialogue
// noise.
//
// Run against a FRESH scripts/build_data.csx output (build/data_final.win),
// not a raw pristine file - see locale/README.md's "Generating/updating
// undertale.pot" section for why: some keys (the essaystuff trigger words)
// only exist once build_data.csx/patches/*.patch add them, not in any
// pristine file's own gml_Script_textdata_en. (settings_language_<code> is
// also only present in a fresh build, but is deliberately skipped below -
// it never goes through po translation, see that check for why.)
//
// Usage:
//   UndertaleModCli/UndertaleModCli load build/steam_data.win \
//     --scripts scripts/build_data.csx -o build/data_final.win -f
//   UndertaleModCli/UndertaleModCli load build/data_final.win \
//     --scripts scripts/extract_pot.csx
//
// Output path is controlled by the POT_OUT env var (default: locale/undertale.pot).

#load "lib/PoFile.csx"
#load "lib/GmlText.csx"

using System;
using System.IO;
using System.Linq;
using UndertaleModLib.Decompiler;

EnsureDataLoaded();

string outPath = Environment.GetEnvironmentVariable("POT_OUT")
    ?? Path.Join(Directory.GetCurrentDirectory(), "locale", "undertale.pot");

var textdataEn = Data.Code.ByName("gml_Script_textdata_en");
if (textdataEn == null)
    throw new Exception("gml_Script_textdata_en not found - is this really an Undertale (Steam) data.win?");

var context = new GlobalDecompileContext(Data);
Func<UndertaleModLib.Models.UndertaleCode, GlobalDecompileContext, string> getText =
    (code, ctx) => GetDecompiledText(code, ctx, null);
var entries = GmlText.ParseTextData(textdataEn, context, getText);

var seen = new System.Collections.Generic.HashSet<string>();
var pot = new PoFile();
pot.Header["Project-Id-Version"] = "Undertale";
pot.Header["Content-Type"] = "text/plain; charset=UTF-8";
pot.Header["MIME-Version"] = "1.0";
pot.Header["Content-Transfer-Encoding"] = "8bit";
pot.Header["POT-Creation-Date"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + "+0000";

foreach (var kv in entries)
{
    // settings_language_<code> is synthetic (only present because we run
    // against a fresh build_data.csx output - see the usage note above) and
    // never goes through po msgid/msgstr translation: build_data.csx always
    // sources its value straight from that locale's own X-Display-Name
    // header, both for the locale's own label and for the fallback shown
    // while browsing the language list in a different active language.
    // Including it here would invite translating something the build
    // ignores, so skip it.
    if (kv.Key.StartsWith("settings_language_")) continue;
    if (!seen.Add(kv.Value)) continue; // dedup: one translation applies to every occurrence
    pot.Entries.Add(new PoEntry { MsgId = kv.Value, MsgStr = "" });
}

Directory.CreateDirectory(Path.GetDirectoryName(outPath));
pot.Save(outPath);
ScriptMessage($"Wrote {pot.Entries.Count} unique strings (from {entries.Count} keys) to {outPath}");
