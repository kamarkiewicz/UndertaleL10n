// Applies a locale's translations (locale/<code>.po) and the shared font
// glyph replacements (fonts/*.png + glyphs_*.csv, shared across all locales)
// to the currently loaded data.win. Save the result with UndertaleModCli's
// own `-o` flag; this script only mutates Data in memory.
//
// Usage:
//   UTLOC_LOCALE=pl_PL UndertaleModCli/UndertaleModCli load <target_data.win> \
//     --scripts scripts/apply_locale.csx -o out.win -f
//
// UTLOC_LOCALE defaults to "pl_PL". UTLOC_ROOT defaults to this repo's root
// (set it if you copy these scripts elsewhere).

#load "lib/PoFile.csx"

using System;
using System.IO;
using System.Linq;
using UndertaleModLib.Util;

EnsureDataLoaded();

string root = Environment.GetEnvironmentVariable("UTLOC_ROOT")
    ?? Directory.GetCurrentDirectory();
string locale = Environment.GetEnvironmentVariable("UTLOC_LOCALE") ?? "pl_PL";
string poPath = Path.Join(root, "locale", locale + ".po");
string fontsDir = Path.Join(root, "fonts");

// --- 1. Strings ---------------------------------------------------------

int stringsReplaced = 0;
if (File.Exists(poPath))
{
    var po = PoFile.Load(poPath);
    var map = po.ToTranslationMap();
    foreach (var s in Data.Strings)
    {
        if (s.Content != null && map.TryGetValue(s.Content, out string translated))
        {
            s.Content = translated;
            stringsReplaced++;
        }
    }
    System.Console.WriteLine($"[{locale}] Replaced {stringsReplaced} strings ({map.Count} available in {poPath}).");
}
else
{
    System.Console.WriteLine($"[{locale}] No .po file found at {poPath}, skipping string patch.");
}

// --- 2. Fonts ------------------------------------------------------------

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
            System.Console.WriteLine($"[{locale}] SKIP font '{name}': no matching glyphs_{name}.csv");
            continue;
        }

        UndertaleFont font = Data.Fonts.ByName(name);
        if (font == null)
        {
            System.Console.WriteLine($"[{locale}] SKIP font '{name}': not present in this data file");
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
                System.Console.WriteLine($"[{locale}] WARNING: glyphs_{name}.csv had some invalid lines");
        }

        fontsUpdated++;
        System.Console.WriteLine($"[{locale}] Updated font '{name}': {font.Glyphs.Count} glyphs, range {font.RangeStart}-{font.RangeEnd}");
    }
    System.Console.WriteLine($"[{locale}] Fonts updated: {fontsUpdated}");
}
else
{
    System.Console.WriteLine($"[{locale}] No fonts/ directory at {fontsDir}, skipping font patch.");
}

ScriptMessage($"Locale '{locale}' applied: {stringsReplaced} strings.");
