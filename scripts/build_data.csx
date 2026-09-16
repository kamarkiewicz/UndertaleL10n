// Builds a data.win with every locale/<code>.po selectable at runtime from
// Settings > Language, instead of overwriting English text.
//
// How this works (see scripts/lib/GmlText.csx for the low-level details):
// Undertale already ships English+Japanese via its own text lookup system
// (scr_gettext(text_id) -> global.text_data_en / global.text_data_<lang>,
// populated by gml_Script_textdata_en / gml_Script_textdata_<lang>). Vanilla
// hardcodes en/ja and a simple EN<->JA toggle in the Settings menu; there's
// no generic multi-language plumbing built in (an earlier assumption to the
// contrary, based on inspecting a third-party Spanish patch, turned out to
// be that patch's own addition, not a base-game feature). This script adds
// that plumbing itself:
//   - compiles one gml_Script_textdata_<code> per locale with a translation,
//   - eagerly executes it at startup (obj_time_Create_0), alongside en/ja,
//   - builds global.lang_list with one entry per locale,
//   - replaces the Settings menu's EN<->JA toggle with generic array
//     cycling over global.lang_list (obj_settingsmenu_Draw_0), and seeds
//     the instance's lang_choose (obj_settingsmenu_Create_0).
// Fonts (fonts/*.png + glyphs_*.csv) are applied once, shared by all locales.
//
// Each locale is one locale/<code>.po file - <code> (e.g. "pl", "es") is
// used directly as the GameMaker language code / textdata_<code> suffix,
// no separate registry needed. The Settings menu label comes from the
// po's own "X-Display-Name" header (e.g. "Polski", "Español").
//
// The Settings-menu/scr_gettext plumbing above is locale-count-independent,
// so it lives as plain unified diffs in patches/ (one per code entry,
// applied via the system `patch` binary - see patches/README.md) instead of
// being hardcoded here. Only obj_time_Create_0's per-locale lang_list/
// script_execute lines are inherently dynamic (they depend on which
// locale/*.po files exist) and stay generated below.
//
// Usage:
//   UndertaleModCli/UndertaleModCli load <pristine_data.win> \
//     --scripts scripts/build_data.csx -o data.win -f
//
// UTLOC_ROOT defaults to this repo's root (set it if you copy these scripts
// elsewhere).

#load "lib/PoFile.csx"
#load "lib/GmlText.csx"
#load "lib/GmlPatch.csx"
#load "lib/SpriteImport.csx"

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UndertaleModLib.Compiler;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Util;

EnsureDataLoaded();

string root = Environment.GetEnvironmentVariable("UTLOC_ROOT")
    ?? Directory.GetCurrentDirectory();
string fontsDir = Path.Join(root, "fonts");

// --- 1. Fonts (shared by all locales, applied once) ----------------------

if (Directory.Exists(fontsDir))
{
    int lastTexPage = Data.EmbeddedTextures.Count - 1;
    int lastTexItem = Data.TexturePageItems.Count - 1;
    int fontsUpdated = 0;

    foreach (string pngPath in Directory.GetFiles(fontsDir, "*.png").OrderBy(p => p))
    {
        string name = Path.GetFileNameWithoutExtension(pngPath);
        string csvPath = Path.Join(fontsDir, "glyphs_" + name + ".csv");
        if (!File.Exists(csvPath))
        {
            System.Console.WriteLine($"[fonts] SKIP font '{name}': no matching glyphs_{name}.csv");
            continue;
        }

        UndertaleFont font = Data.Fonts.ByName(name);
        if (font == null)
        {
            System.Console.WriteLine($"[fonts] SKIP font '{name}': not present in this data file");
            continue;
        }

        GMImage image = GMImage.FromPng(File.ReadAllBytes(pngPath));

        UndertaleEmbeddedTexture texture = new UndertaleEmbeddedTexture();
        texture.Name = new UndertaleString($"Texture {++lastTexPage}");
        texture.TextureData.Image = image;
        Data.EmbeddedTextures.Add(texture);

        UndertaleTexturePageItem pageItem = new UndertaleTexturePageItem();
        pageItem.Name = new UndertaleString($"PageItem {++lastTexItem}");
        pageItem.SourceX = 0;
        pageItem.SourceY = 0;
        pageItem.SourceWidth = (ushort)image.Width;
        pageItem.SourceHeight = (ushort)image.Height;
        pageItem.TargetX = 0;
        pageItem.TargetY = 0;
        pageItem.TargetWidth = (ushort)image.Width;
        pageItem.TargetHeight = (ushort)image.Height;
        pageItem.BoundingWidth = (ushort)image.Width;
        pageItem.BoundingHeight = (ushort)image.Height;
        pageItem.TexturePage = texture;
        Data.TexturePageItems.Add(pageItem);

        font.Texture = pageItem;

        using (StreamReader reader = new StreamReader(csvPath))
        {
            font.Glyphs.Clear();
            string line;
            int head = 0;
            bool hadError = false;
            while ((line = reader.ReadLine()) != null)
            {
                string[] s = line.Split(';');
                if (s.All(x => x.Length == 0)) continue;
                try
                {
                    if (head == 1)
                    {
                        font.RangeStart = UInt16.Parse(s[0]);
                        head++;
                    }
                    if (head == 0)
                    {
                        string namae = s[0].Replace("\"", "");
                        font.DisplayName = Data.Strings.MakeString(namae);
                        font.EmSize = UInt16.Parse(s[1]);
                        font.Bold = Boolean.Parse(s[2]);
                        font.Italic = Boolean.Parse(s[3]);
                        font.Charset = Byte.Parse(s[4]);
                        font.AntiAliasing = Byte.Parse(s[5]);
                        font.ScaleX = UInt16.Parse(s[6]);
                        font.ScaleY = UInt16.Parse(s[7]);
                        head++;
                    }
                    if (head > 1)
                    {
                        font.Glyphs.Add(new UndertaleFont.Glyph()
                        {
                            Character = UInt16.Parse(s[0]),
                            SourceX = UInt16.Parse(s[1]),
                            SourceY = UInt16.Parse(s[2]),
                            SourceWidth = UInt16.Parse(s[3]),
                            SourceHeight = UInt16.Parse(s[4]),
                            Shift = Int16.Parse(s[5]),
                            Offset = Int16.Parse(s[6]),
                        });
                        font.RangeEnd = UInt32.Parse(s[0]);
                    }
                }
                catch { hadError = true; }
            }
            if (hadError)
                System.Console.WriteLine($"[fonts] WARNING: glyphs_{name}.csv had some invalid lines");
        }

        fontsUpdated++;
        System.Console.WriteLine($"[fonts] Updated font '{name}': {font.Glyphs.Count} glyphs, range {font.RangeStart}-{font.RangeEnd}");
    }
    System.Console.WriteLine($"[fonts] Fonts updated: {fontsUpdated}");
}
else
{
    System.Console.WriteLine($"[fonts] No fonts/ directory at {fontsDir}, skipping font patch.");
}

// --- 1b. Translated UI sprites (button/sign graphics with baked-in text) --

string spritesDir = Path.Join(root, "sprites");
if (Directory.Exists(spritesDir))
{
    int spritesImported = SpriteImport.ImportAll(Data, spritesDir);
    System.Console.WriteLine($"[sprites] Imported {spritesImported} translated sprite(s).");
}
else
{
    System.Console.WriteLine($"[sprites] No sprites/ directory at {spritesDir}, skipping sprite import.");
}

// --- 2. Discover locales (locale/<code>.po) + the game's own English key/text table -----

var context = new GlobalDecompileContext(Data);
Func<UndertaleModLib.Models.UndertaleCode, GlobalDecompileContext, string> getText =
    (code, ctx) => GetDecompiledText(code, ctx, null);

var textdataEnCode = Data.Code.ByName("gml_Script_textdata_en");
if (textdataEnCode == null)
    throw new Exception("gml_Script_textdata_en not found - is this really an Undertale (Steam) data.win?");
var keyToEnglish = GmlText.ParseTextData(textdataEnCode, context, getText);
System.Console.WriteLine($"[textdata] {keyToEnglish.Count} keys in gml_Script_textdata_en.");

record LocaleEntry(string PoPath, string Code, string DisplayName);

string localeDir = Path.Join(root, "locale");
var locales = new List<LocaleEntry>();
foreach (var poPath in Directory.GetFiles(localeDir, "*.po").OrderBy(p => p))
{
    string code = Path.GetFileNameWithoutExtension(poPath);
    var po = PoFile.Load(poPath);
    if (!po.Header.TryGetValue("X-Display-Name", out string displayName) || string.IsNullOrWhiteSpace(displayName))
        throw new Exception($"{poPath} is missing an 'X-Display-Name' header (needed as the Settings menu label).");
    locales.Add(new LocaleEntry(poPath, code, displayName));
}
System.Console.WriteLine($"[locales] {locales.Count} locale(s) found in {localeDir}: {string.Join(", ", locales.Select(l => l.Code))}");

// --- 3. Compile one gml_Script_textdata_<code> per locale -----------------

var group = new CodeImportGroup(Data, context, null);
group.AutoCreateAssets = true;

// English defaults for the custom_mtt_essaywords_<N> keys that
// patches/gml_Object_obj_essaystuff_Draw_0.patch introduces (Mettaton's
// essay-rating minigame, originally ~67 hardcoded English trigger words -
// see that patch and README.md's "Architecture" section). These MUST be
// seeded into gml_Script_textdata_en whenever that patch is applied:
// scr_gettext falls back to "" for a missing key, and string_pos("", ...)
// matches everything, which would fire every reaction on every essay.
string[] essayWordsEnglish = {
    "beaut", "hot", "sexy", "pretty", "handsome", "gorgeous", "sparkl",
    "charm", "attract", "cute", "smokin", "elegant", "good look",
    "goodlook", "good-look", "grace", "comely", "fine", "foxy", "looker",
    "dreamboat", "stun", "shapely", "ravishing", "allur", "entic",
    "seduct", "enchant", "appeal", "tantaliz", "adorable", "radiant",
    "capitvat", "leg", "arm", "hair", "personality", "voice", "dancing",
    "dance", "ugly", "hideous", "repulsive", "unattractive", "look bad",
    "stupid", "idiot", "jerk", "asshole", "loser", "dumbass", "douche",
    "creep", "i love you", "i love your", "toby", "fuck", "shit", "cock",
    "pussy", "penis", "vagina", "anus", "poop", "tity", "titty", "bepis",
};

var settingsLanguageAppend = new StringBuilder();
var bundledCodes = new List<string>();

foreach (var locale in locales)
{
    var po = PoFile.Load(locale.PoPath);
    var englishToTranslated = po.ToTranslationMap();

    var sb = new StringBuilder();
    sb.Append("global.text_data_").Append(locale.Code).Append(" = ds_map_create();\n");
    int emitted = 0;
    foreach (var kv in keyToEnglish)
    {
        string translated;
        if (!englishToTranslated.TryGetValue(kv.Value, out translated)) continue;
        sb.Append("ds_map_add(global.text_data_").Append(locale.Code).Append(", \"")
          .Append(kv.Key).Append("\", ").Append(GmlText.EncodeLiteral(translated)).Append(");\n");
        emitted++;
    }
    // Own-language label for the Settings menu (e.g. "es" -> shows "Español"
    // while Spanish itself is the active language).
    sb.Append("ds_map_add(global.text_data_").Append(locale.Code).Append(", \"settings_language_")
      .Append(locale.Code).Append("\", ").Append(GmlText.EncodeLiteral(locale.DisplayName)).Append(");\n");

    // custom_mtt_essaywords_<N> aren't in keyToEnglish (they're not vanilla
    // textdata_en keys - patches/gml_Object_obj_essaystuff_Draw_0.patch and
    // the English defaults above add them earlier in this same build), so
    // they need their own pass here against the same po translation map.
    for (int i = 0; i < essayWordsEnglish.Length; i++)
    {
        string translated;
        if (!englishToTranslated.TryGetValue(essayWordsEnglish[i], out translated)) continue;
        sb.Append("ds_map_add(global.text_data_").Append(locale.Code).Append(", \"custom_mtt_essaywords_")
          .Append(i + 1).Append("\", ").Append(GmlText.EncodeLiteral(translated)).Append(");\n");
        emitted++;
    }

    if (emitted == 0)
    {
        System.Console.WriteLine($"[{locale.Code}] SKIP: no translated strings in {locale.PoPath}.");
        continue;
    }

    group.QueueReplace("gml_Script_textdata_" + locale.Code, sb.ToString());
    bundledCodes.Add(locale.Code);
    System.Console.WriteLine($"[{locale.Code}] {emitted}/{englishToTranslated.Count} translated strings emitted ({locale.PoPath}).");

    // Fallback label for browsing the language list from another active
    // language (e.g. seeing "Polski" while the UI is currently in English).
    settingsLanguageAppend.Append("ds_map_add(global.text_data_en, \"settings_language_")
        .Append(locale.Code).Append("\", ").Append(GmlText.EncodeLiteral(locale.DisplayName)).Append(");\n");
}

if (bundledCodes.Count == 0)
{
    System.Console.WriteLine("[build] No locales had translated content - nothing to bundle.");
}
else
{
    for (int i = 0; i < essayWordsEnglish.Length; i++)
    {
        settingsLanguageAppend.Append("ds_map_add(global.text_data_en, \"custom_mtt_essaywords_")
            .Append(i + 1).Append("\", ").Append(GmlText.EncodeLiteral(essayWordsEnglish[i])).Append(");\n");
    }
    group.QueueAppend("gml_Script_textdata_en", settingsLanguageAppend.ToString());

    // --- 4. Language-selection plumbing (obj_time_Create_0 / obj_settingsmenu_*) ---
    // Vanilla only knows "en"/"ja" and toggles between them; there's no
    // generic language list or menu cycling built in (see file header).

    var langListLines = new StringBuilder();
    langListLines.Append("global.lang_list[0] = \"en\";\nglobal.lang_list[1] = \"ja\";\n");
    int idx = 2;
    foreach (var code in bundledCodes)
    {
        langListLines.Append("global.lang_list[").Append(idx).Append("] = \"").Append(code).Append("\";\n");
        langListLines.Append("script_execute(textdata_").Append(code).Append(");\n");
        idx++;
    }
    group.QueueAppend("gml_Object_obj_time_Create_0", langListLines.ToString());

    // The rest of the plumbing (menu cycling, scr_gettext's language lookup,
    // seeding lang_choose) doesn't depend on which/how many locales are
    // bundled, so it's expressed as plain unified diffs in patches/ instead
    // of hardcoded here - see patches/README.md.
    string patchesDir = Path.Join(root, "patches");
    foreach (string patchPath in Directory.GetFiles(patchesDir, "*.patch").OrderBy(p => p))
    {
        string entryName = Path.GetFileNameWithoutExtension(patchPath);
        var code = Data.Code.ByName(entryName);
        if (code == null)
            throw new Exception($"{patchPath}: no code entry named '{entryName}' in this data.win.");

        string original = getText(code, context);
        string patched = GmlPatch.Apply(patchPath, original);
        group.QueueReplace(entryName, patched);
        System.Console.WriteLine($"[patches] Applied {Path.GetFileName(patchPath)} to {entryName}.");
    }
}

var result = group.Import(true);
System.Console.WriteLine($"[build] Import result: {result}");
ScriptMessage($"Built data with locales: {string.Join(", ", bundledCodes)}");
