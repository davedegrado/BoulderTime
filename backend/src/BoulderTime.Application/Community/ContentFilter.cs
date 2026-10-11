using System.Globalization;
using System.Text;

namespace BoulderTime.Application.Community;

/// <summary>
/// Turns away comments and captions with slurs, hate, sexual terms or insults aimed at a person, before anyone else can
/// read them (App Store guideline 1.2: a method for filtering objectionable material). Reports, blocking and the
/// staff's moderation handle the rest; this is the first line, not the only one.
///
/// Whole words only, after folding case, accents, look-alike digits and symbols (0→o, 4→a, @→a, $→s…), and stretched
/// letters ("stronzooo"); letters spelled out one by one ("s t r o n z o") are joined back up. Plain swearing about a
/// boulder ("this crux is shit") is left to the community, as is anything that names an innocent word: the lists
/// avoid stems that would catch "night", "niggle", "negroni", "zoccolo", "dyke" (the rock) or "fag" (a cigarette).
/// The lists are meant to be edited: add a word, add a test.
/// </summary>
public static class ContentFilter
{
    public const string Message = "This contains words that aren't allowed here. Please rephrase it.";

    /// <summary>Whole words, in their folded form.</summary>
    private static readonly HashSet<string> Words = new(StringComparer.Ordinal)
    {
        // Italian
        "troia", "troie", "zoccola", "zoccole", "negro", "negri", "negra", "negre", "ricchione", "ricchioni",
        "culattone", "culattoni", "pompino", "pompini", "sborra", "ammazzati", "suicidati", "impiccati",
        // English
        "nigger", "niggers", "nigga", "niggas", "twat", "twats", "kys", "tranny",
        "spastic", "spaz",
    };

    /// <summary>Beginnings of words: every form of these is out (stronzo, stronza, stronzi…).</summary>
    private static readonly string[] Prefixes =
    [
        // Italian
        "stronz", "coglion", "puttan", "vaffancul", "fancul", "froci", "frocio", "mongoloid", "pedofil", "cornut",
        // English
        "fagg", "cunt", "motherfuck", "retard", "bitch", "slut", "whore", "dickhead", "asshole", "paedo", "pedophil",
    ];

    /// <summary>Several words in a row.</summary>
    private static readonly string[][] Phrases =
    [
        ["fuck", "you"], ["fuck", "off"], ["fuck", "u"], ["kill", "yourself"], ["kill", "urself"], ["go", "die"],
        ["figlio", "di", "puttana"], ["testa", "di", "cazzo"], ["pezzo", "di", "merda"], ["piece", "of", "shit"],
        ["vai", "a", "morire"],
    ];

    public static bool IsObjectionable(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var tokens = Tokens(text);
        foreach (var token in tokens)
        {
            if (Words.Contains(token)) return true;
            foreach (var prefix in Prefixes) if (token.StartsWith(prefix, StringComparison.Ordinal)) return true;
        }
        foreach (var phrase in Phrases)
            for (var i = 0; i + phrase.Length <= tokens.Count; i++)
                if (phrase.Select((w, k) => tokens[i + k] == w).All(x => x)) return true;
        return false;
    }

    /// <summary>The text as folded words, plus single letters spelled out in a row joined into one word.</summary>
    internal static List<string> Tokens(string text)
    {
        var folded = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            folded.Append(c switch
            {
                '0' => 'o', '1' => 'i', '3' => 'e', '4' => 'a', '5' => 's', '7' => 't', '@' => 'a', '$' => 's',
                _ => c,
            });
        }

        var words = new List<string>();
        var current = new StringBuilder();
        void Flush() { if (current.Length > 0) { words.Add(Squeeze(current.ToString())); current.Clear(); } }
        foreach (var c in folded.ToString())
        {
            if (char.IsLetter(c)) current.Append(c);
            else Flush();
        }
        Flush();

        // "s t r o n z o", "s.t.r.o.n.z.o": runs of three or more single letters become one word too.
        var tokens = new List<string>(words);
        for (var i = 0; i < words.Count;)
        {
            var j = i;
            while (j < words.Count && words[j].Length == 1) j++;
            if (j - i >= 3) tokens.Add(string.Concat(words.Skip(i).Take(j - i)));
            i = Math.Max(j, i + 1);
        }
        return tokens;
    }

    /// <summary>Three or more of the same letter count as two: "stronzooo" → "stronzoo", "puttttana" → "puttana".</summary>
    private static string Squeeze(string word)
    {
        var sb = new StringBuilder(word.Length);
        foreach (var c in word)
        {
            var n = sb.Length;
            if (n >= 2 && sb[n - 1] == c && sb[n - 2] == c) continue;
            sb.Append(c);
        }
        return sb.ToString();
    }
}
