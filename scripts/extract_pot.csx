// Extracts the game's translatable text into a .pot template (empty
// msgstr), sourced from gml_Script_textdata_en - the GML script that
// populates global.text_data_en, the lookup table scr_gettext() reads at
// runtime (see scripts/lib/GmlText.csx for how this system works). This is
// every real, in-game, user-facing string - unlike a raw Data.Strings dump,
// it doesn't include function/variable/asset names and other non-dialogue
// noise.
//
// Run against a PRISTINE (untranslated) data.win so the template reflects
// the current official game text.
//
// Usage:
//   UndertaleModCli/UndertaleModCli load <pristine_data.win> \
//     --scripts scripts/extract_pot.csx
//
// Output path is controlled by the POT_OUT env var (default: locale/undertale.pot).

#load "lib/PoFile.csx"
#load "lib/GmlText.csx"

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UndertaleModLib.Decompiler;

EnsureDataLoaded();

string outPath = Environment.GetEnvironmentVariable("POT_OUT")
    ?? Path.Join(Directory.GetCurrentDirectory(), "locale", "undertale.pot");

var gi = Data.GeneralInfo;
string gameVersion = $"{gi.Major}.{gi.Minor}.{gi.Release}.{gi.Build}";

string sourceHash;
using (var sha256 = SHA256.Create())
using (var stream = File.OpenRead(FilePath))
    sourceHash = Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();

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
pot.Header["X-Game-Data-Version"] = gameVersion; // Data.GeneralInfo Major.Minor.Release.Build of the data.win this was extracted from
pot.Header["X-Game-Data-SHA256"] = sourceHash; // SHA-256 of the exact data.win file this was extracted from

foreach (var kv in entries)
{
    if (!seen.Add(kv.Value)) continue; // dedup: one translation applies to every occurrence
    pot.Entries.Add(new PoEntry { MsgId = kv.Value, MsgStr = "" });
}

Directory.CreateDirectory(Path.GetDirectoryName(outPath));
pot.Save(outPath);
ScriptMessage($"Wrote {pot.Entries.Count} unique strings (from {entries.Count} keys) to {outPath}");
