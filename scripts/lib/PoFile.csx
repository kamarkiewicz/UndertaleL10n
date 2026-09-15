// Minimal, dependency-free reader/writer for the GNU gettext .po/.pot format.
// Supports what we actually need: msgid/msgstr pairs, #, fuzzy flags, and
// #. extracted comments. Not a full gettext implementation, but produces
// files that open correctly in Poedit/Lokalize and round-trip losslessly
// for our own reader.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class PoEntry
{
    public string MsgId = "";
    public string MsgStr = "";
    public bool Fuzzy = false;
    public string Comment = null; // optional "#. ..." extracted comment
}

public class PoFile
{
    public Dictionary<string, string> Header = new Dictionary<string, string>();
    public List<PoEntry> Entries = new List<PoEntry>();

    public static string EscapeString(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    public static string UnescapeString(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                char next = s[++i];
                switch (next)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    default: sb.Append('\\'); sb.Append(next); break;
                }
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    // Extracts the content of a `keyword "quoted string"` line, or a bare
    // continuation `"quoted string"` line. Returns null if the line doesn't
    // start with a quote after the optional keyword+space.
    private static string ExtractQuoted(string line)
    {
        int firstQuote = line.IndexOf('"');
        int lastQuote = line.LastIndexOf('"');
        if (firstQuote < 0 || lastQuote <= firstQuote) return null;
        return UnescapeString(line.Substring(firstQuote + 1, lastQuote - firstQuote - 1));
    }

    public static PoFile Load(string path)
    {
        var po = new PoFile();
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);

        bool fuzzy = false;
        string comment = null;
        string msgid = null;
        StringBuilder msgidBuf = null;
        StringBuilder msgstrBuf = null;
        bool inMsgId = false, inMsgStr = false;
        bool isHeaderEntry = false;

        void FlushEntry()
        {
            if (msgidBuf == null) return;
            string id = msgidBuf.ToString();
            string str = msgstrBuf?.ToString() ?? "";
            if (id.Length == 0)
            {
                // Header entry: msgstr is a "key: value\n" blob.
                foreach (var rawLine in str.Split('\n'))
                {
                    int idx = rawLine.IndexOf(':');
                    if (idx > 0)
                        po.Header[rawLine.Substring(0, idx).Trim()] = rawLine.Substring(idx + 1).Trim();
                }
            }
            else
            {
                po.Entries.Add(new PoEntry { MsgId = id, MsgStr = str, Fuzzy = fuzzy, Comment = comment });
            }
            fuzzy = false;
            comment = null;
            msgidBuf = null;
            msgstrBuf = null;
            inMsgId = false;
            inMsgStr = false;
        }

        foreach (string raw in lines)
        {
            string line = raw.TrimEnd();
            if (line.Length == 0)
            {
                FlushEntry();
                continue;
            }
            if (line.StartsWith("#,"))
            {
                if (line.Contains("fuzzy")) fuzzy = true;
                continue;
            }
            if (line.StartsWith("#."))
            {
                comment = line.Substring(2).Trim();
                continue;
            }
            if (line.StartsWith("#"))
            {
                continue; // translator/reference comments we don't otherwise track
            }
            if (line.StartsWith("msgid "))
            {
                FlushEntry();
                msgidBuf = new StringBuilder(ExtractQuoted(line) ?? "");
                inMsgId = true;
                inMsgStr = false;
                continue;
            }
            if (line.StartsWith("msgstr "))
            {
                msgstrBuf = new StringBuilder(ExtractQuoted(line) ?? "");
                inMsgId = false;
                inMsgStr = true;
                continue;
            }
            if (line.StartsWith("\""))
            {
                string cont = ExtractQuoted(line) ?? "";
                if (inMsgStr) msgstrBuf.Append(cont);
                else if (inMsgId) msgidBuf.Append(cont);
                continue;
            }
        }
        FlushEntry();

        return po;
    }

    public void Save(string path)
    {
        var sb = new StringBuilder();

        sb.AppendLine("msgid \"\"");
        sb.AppendLine("msgstr \"\"");
        var header = Header.Count > 0 ? Header : new Dictionary<string, string>
        {
            ["Content-Type"] = "text/plain; charset=UTF-8",
            ["MIME-Version"] = "1.0",
            ["Content-Transfer-Encoding"] = "8bit",
        };
        foreach (var kv in header)
            sb.AppendLine($"\"{EscapeString(kv.Key)}: {EscapeString(kv.Value)}\\n\"");
        sb.AppendLine();

        foreach (var e in Entries)
        {
            if (e.Comment != null)
                sb.AppendLine($"#. {e.Comment}");
            if (e.Fuzzy)
                sb.AppendLine("#, fuzzy");
            sb.AppendLine($"msgid \"{EscapeString(e.MsgId)}\"");
            sb.AppendLine($"msgstr \"{EscapeString(e.MsgStr)}\"");
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    // Dictionary of msgid -> msgstr, skipping untranslated (empty msgstr) entries.
    public Dictionary<string, string> ToTranslationMap()
    {
        var map = new Dictionary<string, string>();
        foreach (var e in Entries)
        {
            if (!string.IsNullOrEmpty(e.MsgStr))
                map[e.MsgId] = e.MsgStr;
        }
        return map;
    }
}
