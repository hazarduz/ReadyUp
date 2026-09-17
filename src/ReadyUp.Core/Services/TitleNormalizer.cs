using System.Text;
using System.Text.RegularExpressions;

namespace ReadyUp.Core.Services;

/// <summary>Turns raw filenames/registry names into presentable titles and sort keys.</summary>
public static partial class TitleNormalizer
{
    private static readonly string[] LeadingArticles = { "the ", "a ", "an " };

    /// <summary>"the_witcher_3-wild_hunt" → "The Witcher 3 Wild Hunt".</summary>
    public static string ToDisplayTitle(string raw)
    {
        var withoutExt = Path.GetFileNameWithoutExtension(raw);
        var spaced = SeparatorRegex().Replace(withoutExt, " ");
        spaced = CamelCaseRegex().Replace(spaced, "$1 $2");
        spaced = Regex.Replace(spaced, @"\s+", " ").Trim();

        return CultureInfoTitleCase(spaced);
    }

    /// <summary>"The Witcher 3" → "Witcher 3, The" for stable alphabetical sorting.</summary>
    public static string ToSortingTitle(string displayTitle)
    {
        foreach (var article in LeadingArticles)
        {
            if (displayTitle.StartsWith(article, StringComparison.OrdinalIgnoreCase))
            {
                var rest = displayTitle[article.Length..];
                var articleWord = displayTitle[..article.Length].Trim();
                return $"{rest}, {articleWord}";
            }
        }

        return displayTitle;
    }

    private static string CultureInfoTitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;

        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var word in words)
        {
            if (sb.Length > 0) sb.Append(' ');

            // Preserve all-caps acronyms/roman numerals (e.g. "III", "VR") as-is.
            if (word.Length > 1 && word.All(char.IsUpper))
            {
                sb.Append(word);
                continue;
            }

            sb.Append(char.ToUpperInvariant(word[0]));
            if (word.Length > 1) sb.Append(word[1..].ToLowerInvariant());
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"[_\.\-]+")]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex(@"(\p{Ll})(\p{Lu})")]
    private static partial Regex CamelCaseRegex();
}
