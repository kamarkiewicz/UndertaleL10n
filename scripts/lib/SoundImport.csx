// Imports translated embedded sound effects (voice lines with a joke/pun
// that doesn't translate as text - e.g. Flowey's "that's a wonderful
// idea!") from sounds/<base_name>_<code>.wav into the loaded data.win as
// new UndertaleSound assets, named exactly <base_name>_<code> so a
// generalized language-lookup patch (see patches/README.md) finds them the
// same way scr_getsprite finds translated sprites.
//
// Only covers embedded sounds (AudioEntryFlags.IsEmbedded) - Undertale also
// streams some audio (background music) from external .ogg files next to
// data.win, which this can't reach; see sounds/README.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UndertaleModLib.Models;

public static class SoundImport
{
    public static int ImportAll(UndertaleData data, string soundsDir)
    {
        if (!Directory.Exists(soundsDir)) return 0;
        int imported = 0;

        foreach (string wavPath in Directory.GetFiles(soundsDir, "*.wav").OrderBy(p => p))
        {
            string name = Path.GetFileNameWithoutExtension(wavPath); // e.g. snd_wonderfulidea_it
            int lastUnderscore = name.LastIndexOf('_');
            if (lastUnderscore < 0)
            {
                System.Console.WriteLine($"[sounds] SKIP {name}: expected <base_name>_<code>.wav naming.");
                continue;
            }
            string baseName = name.Substring(0, lastUnderscore);

            var baseSound = data.Sounds.FirstOrDefault(s => s.Name?.Content == baseName);
            if (baseSound == null)
            {
                System.Console.WriteLine($"[sounds] SKIP {name}: base sound '{baseName}' not found.");
                continue;
            }

            var embeddedAudio = new UndertaleEmbeddedAudio { Data = File.ReadAllBytes(wavPath) };
            data.EmbeddedAudio.Add(embeddedAudio);

            var sound = new UndertaleSound
            {
                Name = data.Strings.MakeString(name),
                Flags = baseSound.Flags,
                Type = baseSound.Type,
                File = data.Strings.MakeString(name + ".wav"),
                Effects = baseSound.Effects,
                Volume = baseSound.Volume,
                Preload = baseSound.Preload,
                Pitch = baseSound.Pitch,
                AudioGroup = baseSound.AudioGroup,
                AudioFile = embeddedAudio,
                AudioID = data.EmbeddedAudio.Count - 1,
                GroupID = baseSound.GroupID,
            };
            data.Sounds.Add(sound);
            imported++;
            System.Console.WriteLine($"[sounds] Imported {name} (from {baseName}).");
        }

        return imported;
    }
}
