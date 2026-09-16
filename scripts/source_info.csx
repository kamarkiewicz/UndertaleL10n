// Writes the loaded file's own version + SHA-256 to SOURCE_INFO_OUT (env
// var), one "key=value" line each. Used by CI to report each platform's
// own pristine source version in release notes, since different
// platforms/storefronts can report different version numbers for what's
// otherwise the same translatable text - see README's "Platforms" section.

using System;
using System.IO;
using System.Security.Cryptography;

EnsureDataLoaded();

string outPath = Environment.GetEnvironmentVariable("SOURCE_INFO_OUT")
    ?? throw new Exception("SOURCE_INFO_OUT env var not set.");

var gi = Data.GeneralInfo;
string version = $"{gi.Major}.{gi.Minor}.{gi.Release}.{gi.Build}";

string hash;
using (var sha256 = SHA256.Create())
using (var stream = File.OpenRead(FilePath))
    hash = Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();

File.WriteAllLines(outPath, new[] {
    $"file={Path.GetFileName(FilePath)}",
    $"version={version}",
    $"sha256={hash}",
});
ScriptMessage($"Wrote source info to {outPath}");
