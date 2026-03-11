using WordGameUk.Multiplayer.Core;
using WordGameUk.Multiplayer.Core.Multiplayer.Domain;

namespace WordGameUk.Application.Dictionary;

public sealed class WordDictionaryService : IWordDictionaryService, IWordDictionary
{
    private readonly HashSet<string> _words;
    private readonly Dictionary<string, List<string>> _fragmentIndex;
    private readonly List<string> _fragmentsAtMost500;
    private readonly List<string> _fragmentsAtLeast500;

    public WordDictionaryService(WordDictionaryFileOptions files)
    {
        _words = new HashSet<string>(StringComparer.Ordinal);
        _fragmentIndex = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        _fragmentsAtMost500 = [];
        _fragmentsAtLeast500 = [];

        EnsureFilteredWords(files.SourcePath, files.FilteredPath);
        Load(files.FilteredPath);
        BuildDifficultyBuckets();
    }

    public bool ContainsWord(string word)
    {
        var normalized = Normalize(word);
        return _words.Contains(normalized);
    }

    public bool ContainsSyllable(string word, string syllable)
    {
        var normalizedWord = Normalize(word);
        var normalizedSyllable = Normalize(syllable);

        return normalizedWord.Contains(normalizedSyllable, StringComparison.Ordinal);
    }

    public IReadOnlyCollection<string> GetCandidates(string syllable)
    {
        var normalized = Normalize(syllable);

        return _fragmentIndex.TryGetValue(normalized, out var words)
            ? words
            : Array.Empty<string>();
    }

    public string GetRandomSyllable(Random random, SyllableDifficulty difficulty)
    {
        var source = difficulty == SyllableDifficulty.AtLeast500
            ? _fragmentsAtLeast500
            : _fragmentsAtMost500;

        if (source.Count == 0)
            return string.Empty;

        return source[random.Next(source.Count)];
    }

    private void Load(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            var word = Normalize(line);

            if (string.IsNullOrWhiteSpace(word))
                continue;

            if (!_words.Add(word))
                continue;

            foreach (var fragment in GetFragments(word))
            {
                if (!_fragmentIndex.TryGetValue(fragment, out var bucket))
                {
                    bucket = new List<string>();
                    _fragmentIndex[fragment] = bucket;
                }

                bucket.Add(word);
            }
        }
    }

    private static void EnsureFilteredWords(string sourcePath, string filteredPath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Dictionary source file not found.", sourcePath);

        if (File.Exists(filteredPath))
        {
            var sourceWriteTime = File.GetLastWriteTimeUtc(sourcePath);
            var filteredWriteTime = File.GetLastWriteTimeUtc(filteredPath);
            if (filteredWriteTime >= sourceWriteTime)
                return;
        }

        using var writer = new StreamWriter(filteredPath, append: false);
        foreach (var line in File.ReadLines(sourcePath))
        {
            if (!TryExtractAllowedWord(line, out var word))
                continue;

            writer.WriteLine(word);
        }
    }

    private static bool TryExtractAllowedWord(string line, out string word)
    {
        word = string.Empty;

        if (string.IsNullOrWhiteSpace(line))
            return false;

        var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return false;

        if (!ShouldIncludeByTags(parts[2]))
            return false;

        word = Normalize(parts[0]);
        return !string.IsNullOrWhiteSpace(word);
    }

    private static bool ShouldIncludeByTags(string tagsValue)
    {
        var tags = tagsValue.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (tags.Length == 0)
            return false;

        if (HasAny(tags, "prop", "geo", "fname", "lname"))
            return false;

        var partOfSpeech = tags[0];

        return partOfSpeech switch
        {
            "verb" => Has(tags, "inf"),
            "noun" => Has(tags, "v_naz") || Has(tags, "nv"),
            "adj" => Has(tags, "v_naz"),
            "adv" => Has(tags, "compb"),
            _ => true
        };
    }

    private static bool Has(string[] tags, string value)
    {
        foreach (var tag in tags)
        {
            if (tag == value)
                return true;
        }

        return false;
    }

    private static bool HasAny(string[] tags, params string[] values)
    {
        foreach (var value in values)
        {
            if (Has(tags, value))
                return true;
        }

        return false;
    }

    private void BuildDifficultyBuckets()
    {
        foreach (var (fragment, words) in _fragmentIndex)
        {
            if (words.Count >= 500)
                _fragmentsAtLeast500.Add(fragment);

            if (words.Count <= 500)
                _fragmentsAtMost500.Add(fragment);
        }
    }

    private static string Normalize(string input)
    {
        return input
            .Trim()
            .ToLowerInvariant()
            .Replace("’", "'")
            .Replace("ʼ", "'")
            .Replace("`", "'");
    }

    private static IEnumerable<string> GetFragments(string word)
    {
        const int minLen = 2;
        const int maxLen = 3;

        for (int len = minLen; len <= maxLen; len++)
        {
            if (word.Length < len)
                continue;

            for (int i = 0; i <= word.Length - len; i++)
            {
                var fragment = word.Substring(i, len);

                if (IsValidFragment(fragment))
                    yield return fragment;
            }
        }
    }

    private static bool IsValidFragment(string fragment)
    {
        foreach (var ch in fragment)
        {
            if (!char.IsLetter(ch))
                return false;
        }

        return true;
    }
}
