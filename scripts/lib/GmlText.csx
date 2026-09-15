// Reads and writes GML string literals as they appear in decompiled
// gml_Script_textdata_<lang> scripts (ds_map_add(global.text_data_en, "key",
// "value"); one per line - see obj_time_Create_0 / scr_gettext).
//
// The GML compiler does NOT process backslash as an escape character at all
// (confirmed by compile->decompile round-trip testing: N backslashes in
// source survive as exactly N backslashes at runtime). It also can't escape
// a quote character inside a same-quoted string - the decompiler avoids
// embedding both quote types by switching style or concatenating with `+`,
// e.g. 'tears just won' + "'" + 't come./%'. Decode/Encode below mirror
// that: no backslash handling, only quote-style bookkeeping.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;

public static class GmlText
{
    private static readonly Regex DsMapAddLine =
        new Regex(@"^ds_map_add\(global\.text_data_\w+, ("".*?""), (.*)\);$");

    // Decodes a single GML string literal (including its surrounding quotes)
    // into its runtime text value. No escape processing - see file header.
    public static string DecodeLiteral(string literalWithQuotes)
    {
        if (literalWithQuotes.Length < 2)
            throw new ArgumentException($"literal too short: {literalWithQuotes}");
        char quote = literalWithQuotes[0];
        if (literalWithQuotes[literalWithQuotes.Length - 1] != quote)
            throw new ArgumentException($"mismatched quote: {literalWithQuotes}");
        return literalWithQuotes.Substring(1, literalWithQuotes.Length - 2);
    }

    // Splits a GML expression like 'a' + "b" + 'c' on top-level `+`
    // operators, respecting quoted string boundaries (no escape handling
    // inside quotes - a quote character always closes the literal).
    public static List<string> SplitTopLevelPlus(string expr)
    {
        var parts = new List<string>();
        var cur = new System.Text.StringBuilder();
        char? inQuote = null;
        foreach (char c in expr)
        {
            if (inQuote != null)
            {
                cur.Append(c);
                if (c == inQuote) inQuote = null;
                continue;
            }
            if (c == '"' || c == '\'')
            {
                inQuote = c;
                cur.Append(c);
                continue;
            }
            if (c == '+')
            {
                var piece = cur.ToString().Trim();
                if (piece.Length > 0) parts.Add(piece);
                cur.Clear();
                continue;
            }
            cur.Append(c);
        }
        var last = cur.ToString().Trim();
        if (last.Length > 0) parts.Add(last);
        return parts;
    }

    public static string DecodeValueExpr(string expr)
    {
        var parts = SplitTopLevelPlus(expr);
        var sb = new System.Text.StringBuilder();
        foreach (var p in parts)
        {
            if (p.Length == 0 || (p[0] != '"' && p[0] != '\''))
                throw new ArgumentException($"unexpected non-literal token: {p} in {expr}");
            sb.Append(DecodeLiteral(p));
        }
        return sb.ToString();
    }

    // Encodes `text` back into a compilable GML value expression (no
    // surrounding assignment, just the right-hand side). Picks a quote
    // style per content run, and concatenates with `+` around any embedded
    // quote character of whichever style is currently open - the same
    // strategy the game's own decompiler uses, since GML can't escape a
    // literal occurrence of its own delimiter.
    public static string EncodeLiteral(string text)
    {
        if (text.Length == 0) return "\"\"";

        var pieces = new List<string>();
        var run = new System.Text.StringBuilder();
        foreach (char c in text)
        {
            if (c == '"' || c == '\'')
            {
                if (run.Length > 0)
                {
                    pieces.Add(EncodeRun(run.ToString()));
                    run.Clear();
                }
                // Lone quote character: wrap in the OTHER quote type.
                pieces.Add(c == '"' ? "'\"'" : "\"'\"");
            }
            else
            {
                run.Append(c);
            }
        }
        if (run.Length > 0) pieces.Add(EncodeRun(run.ToString()));
        return string.Join(" + ", pieces);
    }

    private static string EncodeRun(string run)
    {
        // `run` contains neither `"` nor `'` (those were split out by the
        // caller), so either quote style is always safe here.
        return "\"" + run + "\"";
    }

    // Decompiles `code` and parses every ds_map_add(global.text_data_X, "key", value);
    // line into a key -> text dictionary, preserving insertion order.
    public static Dictionary<string, string> ParseTextData(
        UndertaleCode code, GlobalDecompileContext context,
        Func<UndertaleCode, GlobalDecompileContext, string> getDecompiledText)
    {
        string decompiled = getDecompiledText(code, context);
        var result = new Dictionary<string, string>();
        foreach (var rawLine in decompiled.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r', '\n').Trim();
            if (!line.StartsWith("ds_map_add(")) continue;
            var m = DsMapAddLine.Match(line);
            if (!m.Success)
            {
                System.Console.WriteLine($"[GmlText] WARN: line did not match expected shape: {line.Substring(0, Math.Min(100, line.Length))}");
                continue;
            }
            string key = DecodeLiteral(m.Groups[1].Value);
            string value;
            try { value = DecodeValueExpr(m.Groups[2].Value); }
            catch (Exception e)
            {
                System.Console.WriteLine($"[GmlText] WARN: failed to decode value for key {key}: {e.Message}");
                continue;
            }
            if (!result.ContainsKey(key)) result[key] = value;
        }
        return result;
    }
}
