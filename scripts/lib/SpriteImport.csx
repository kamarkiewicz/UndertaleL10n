// Imports translated UI sprites (button/sign graphics with text baked into
// the pixels - scr_getsprite/asset_get_index can't reach these the way
// scr_gettext reaches plain strings) from sprites/*.png + sprites/sprites.csv
// + sprites/frames.csv into the loaded data.win as new UndertaleSprite
// assets, named exactly as the CSV says (e.g. "spr_fightbt_es") so the
// generalized scr_getsprite (patches/gml_Script_scr_getsprite.patch) finds
// them via asset_get_index(base_name + "_" + global.language) at runtime.
//
// sprites.csv: one row per sprite (name,width,height,origin_x,origin_y,
//   margin_left,margin_right,margin_top,margin_bottom,bbox_mode,frame_count)
// frames.csv: one row per frame (name,frame_index,png_file,source_width,
//   source_height,target_x,target_y,target_width,target_height,
//   bounding_width,bounding_height) - positions/sizes as they were packed
//   in the source data.win's texture page, reproduced exactly so trimmed
//   sprites keep their original alignment.
//
// Collision masks are seeded as a simple fully-solid rectangle (these are
// UI/decorative sprites - buttons, signs - never used in place_meeting-style
// pixel collision, so exact mask data doesn't matter, only that one exists).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UndertaleModLib.Models;
using UndertaleModLib.Util;

public static class SpriteImport
{
    public static int ImportAll(UndertaleData data, string spritesDir)
    {
        string spritesCsvPath = Path.Join(spritesDir, "sprites.csv");
        string framesCsvPath = Path.Join(spritesDir, "frames.csv");
        if (!File.Exists(spritesCsvPath) || !File.Exists(framesCsvPath))
            return 0;

        var framesBySprite = File.ReadAllLines(framesCsvPath).Skip(1)
            .Where(l => l.Length > 0)
            .Select(l => l.Split(','))
            .GroupBy(f => f[0])
            .ToDictionary(g => g.Key, g => g.OrderBy(f => int.Parse(f[1])).ToList());

        int lastTexPage = data.EmbeddedTextures.Count - 1;
        int lastTexItem = data.TexturePageItems.Count - 1;
        int imported = 0;

        foreach (var line in File.ReadAllLines(spritesCsvPath).Skip(1))
        {
            if (line.Length == 0) continue;
            var f = line.Split(',');
            string name = f[0];
            uint width = uint.Parse(f[1]);
            uint height = uint.Parse(f[2]);
            int originX = int.Parse(f[3]);
            int originY = int.Parse(f[4]);
            int marginLeft = int.Parse(f[5]);
            int marginRight = int.Parse(f[6]);
            int marginTop = int.Parse(f[7]);
            int marginBottom = int.Parse(f[8]);
            uint bboxMode = uint.Parse(f[9]);
            int frameCount = int.Parse(f[10]);

            if (!framesBySprite.TryGetValue(name, out var frames) || frames.Count != frameCount)
            {
                System.Console.WriteLine($"[sprites] SKIP {name}: frame metadata missing/mismatched.");
                continue;
            }

            var sprite = new UndertaleSprite
            {
                Name = data.Strings.MakeString(name),
                Width = width,
                Height = height,
                OriginX = originX,
                OriginY = originY,
                MarginLeft = marginLeft,
                MarginRight = marginRight,
                MarginTop = marginTop,
                MarginBottom = marginBottom,
                BBoxMode = bboxMode,
                Transparent = true,
                SepMasks = UndertaleSprite.SepMaskType.AxisAlignedRect,
            };

            foreach (var fr in frames)
            {
                string pngFile = fr[2];
                ushort sourceWidth = ushort.Parse(fr[3]);
                ushort sourceHeight = ushort.Parse(fr[4]);
                ushort targetX = ushort.Parse(fr[5]);
                ushort targetY = ushort.Parse(fr[6]);
                ushort targetWidth = ushort.Parse(fr[7]);
                ushort targetHeight = ushort.Parse(fr[8]);
                ushort boundingWidth = ushort.Parse(fr[9]);
                ushort boundingHeight = ushort.Parse(fr[10]);

                GMImage image = GMImage.FromPng(File.ReadAllBytes(Path.Join(spritesDir, pngFile)));

                var texture = new UndertaleEmbeddedTexture();
                texture.Name = new UndertaleString($"Texture {++lastTexPage}");
                texture.TextureData.Image = image;
                data.EmbeddedTextures.Add(texture);

                var pageItem = new UndertaleTexturePageItem();
                pageItem.Name = new UndertaleString($"PageItem {++lastTexItem}");
                pageItem.SourceX = 0;
                pageItem.SourceY = 0;
                pageItem.SourceWidth = sourceWidth;
                pageItem.SourceHeight = sourceHeight;
                pageItem.TargetX = targetX;
                pageItem.TargetY = targetY;
                pageItem.TargetWidth = targetWidth;
                pageItem.TargetHeight = targetHeight;
                pageItem.BoundingWidth = boundingWidth;
                pageItem.BoundingHeight = boundingHeight;
                pageItem.TexturePage = texture;
                data.TexturePageItems.Add(pageItem);

                sprite.Textures.Add(new UndertaleSprite.TextureEntry { Texture = pageItem });
            }

            sprite.CollisionMasks.Add(MakeSolidMask((int)width, (int)height));

            data.Sprites.Add(sprite);
            imported++;
        }

        return imported;
    }

    static UndertaleSprite.MaskEntry MakeSolidMask(int width, int height)
    {
        int bytesPerRow = (width + 7) / 8;
        var maskData = new byte[bytesPerRow * height];
        for (int y = 0; y < height; y++)
        {
            int bitsLeft = width;
            int rowStart = y * bytesPerRow;
            int b = 0;
            while (bitsLeft > 0)
            {
                int bits = Math.Min(8, bitsLeft);
                maskData[rowStart + b] = (byte)(0xFF << (8 - bits));
                bitsLeft -= bits;
                b++;
            }
        }
        return new UndertaleSprite.MaskEntry { Data = maskData, Width = width, Height = height };
    }
}
