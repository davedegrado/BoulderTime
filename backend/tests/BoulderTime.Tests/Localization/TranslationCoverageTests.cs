using System.Text.RegularExpressions;
using BoulderTime.Application.Localization;

namespace BoulderTime.Tests.Localization;

/// <summary>
/// Every message the API can send back has its Italian. Found by reading the source, so a new message without a
/// translation fails here instead of reaching an Italian reader in English.
/// </summary>
public sealed partial class TranslationCoverageTests
{
    [GeneratedRegex("""\.Check\([^;]*?,\s*"[A-Za-z.]+"\s*,\s*(\$?"(?:[^"\\]|\\.)*")""", RegexOptions.Singleline)]
    private static partial Regex CheckMessage();

    [GeneratedRegex("""new ValidationException\(\s*"[A-Za-z.]+"\s*,\s*(\$?"(?:[^"\\]|\\.)*")""")]
    private static partial Regex ValidationMessage();

    [GeneratedRegex("""new (?:ForbiddenException|ConflictException|UnauthorizedException)\(\s*(\$?"(?:[^"\\]|\\.)*")""")]
    private static partial Regex OtherMessage();

    [GeneratedRegex(@"\{[^{}]+\}")]
    private static partial Regex Hole();

    private static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "BoulderTime.Application"))) dir = dir.Parent;
        return dir is null ? throw new DirectoryNotFoundException("backend/src not found") : Path.Combine(dir.FullName, "src");
    }

    /// <summary>The literal as a reader would see it, with each interpolated value replaced by a number.</summary>
    private static string Sample(string literal)
    {
        var interpolated = literal.StartsWith('$');
        var text = (interpolated ? literal[1..] : literal)[1..^1];
        if (!interpolated) return text;
        var n = 10;
        return Hole().Replace(text, _ => (n++).ToString());
    }

    [Fact]
    public void Every_api_message_has_an_italian_version()
    {
        var root = SourceRoot();
        var files = Directory.GetFiles(Path.Combine(root, "BoulderTime.Application"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(root, "BoulderTime.Api"), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
        var missing = new List<string>();
        var found = 0;
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            foreach (var regex in new[] { CheckMessage(), ValidationMessage(), OtherMessage() })
            {
                foreach (Match m in regex.Matches(source))
                {
                    found++;
                    var sample = Sample(m.Groups[1].Value);
                    if (Translations.Translate(sample, "it") == sample) missing.Add($"{Path.GetFileName(file)}: {sample}");
                }
            }
        }
        found.Should().BeGreaterThan(100, "the scan has to find the messages to mean anything");
        missing.Should().BeEmpty();
    }
}
