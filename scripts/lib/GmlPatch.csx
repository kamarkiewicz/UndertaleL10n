// Applies a unified diff (as produced by `diff -u` / `git diff --no-index`)
// to a decompiled GML string, via the system `patch` binary (present by
// default on macOS and Ubuntu, incl. GitHub Actions' ubuntu-latest runner -
// see patches/README.md for the file convention this expects).

using System;
using System.Diagnostics;
using System.IO;

public static class GmlPatch
{
    public static string Apply(string patchFilePath, string originalText)
    {
        string tempIn = Path.GetTempFileName();
        string tempOut = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempIn, originalText);

            var psi = new ProcessStartInfo
            {
                FileName = "patch",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("--fuzz=0");
            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(tempOut);
            psi.ArgumentList.Add(tempIn);
            psi.ArgumentList.Add(patchFilePath);

            using var proc = Process.Start(psi);
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0)
                throw new Exception(
                    $"'patch' failed applying {patchFilePath} (exit {proc.ExitCode}):\n" +
                    stdout + stderr +
                    "\nThe decompiled GML this patch expects to match has probably " +
                    "drifted (e.g. build/pristine.win was updated) - regenerate the " +
                    "patch against the current vanilla decompile.");

            return File.ReadAllText(tempOut);
        }
        finally
        {
            File.Delete(tempIn);
            File.Delete(tempOut);
        }
    }
}
