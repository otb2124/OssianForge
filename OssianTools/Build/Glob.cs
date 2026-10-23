using System.Text.RegularExpressions;

namespace OssianForge.Tools.Build;

/// <summary>
/// Case-insensitive exclude patterns. "*" and "?" match inside one path segment, "**" matches across segments.
/// A pattern without "/" is matched against the bare file or folder name anywhere in the tree (so "*.pdb" works at
/// any depth); a pattern containing "/" is matched against the path relative to the template root, and a leading
/// "/" says the same thing for a single name ("/Content" = only the top-level Content folder).
/// </summary>
internal sealed class ExcludeSet
{
    private readonly List<(Regex Rx, bool NameOnly)> _rules = new();

    public ExcludeSet(IEnumerable<string> patterns)
    {
        foreach (var raw in patterns)
        {
            string p = raw.Trim().Replace('\\', '/');
            bool anchored = p.StartsWith('/');
            p = p.Trim('/');
            if (p.Length == 0) continue;
            _rules.Add((Compile(p), !anchored && !p.Contains('/')));
        }
    }

    public bool IsExcluded(string relativePath, string name)
    {
        string rel = relativePath.Replace('\\', '/');
        foreach (var (rx, nameOnly) in _rules)
            if (rx.IsMatch(nameOnly ? name : rel)) return true;
        return false;
    }

    private static Regex Compile(string glob)
    {
        var sb = new System.Text.StringBuilder("^");
        for (int i = 0; i < glob.Length; i++)
        {
            char c = glob[i];
            if (c == '*' && i + 1 < glob.Length && glob[i + 1] == '*') { sb.Append(".*"); i++; }
            else if (c == '*') sb.Append("[^/]*");
            else if (c == '?') sb.Append("[^/]");
            else sb.Append(Regex.Escape(c.ToString()));
        }
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}