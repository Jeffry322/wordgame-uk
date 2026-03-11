using WordGameUk.Multiplayer.Core;

namespace WordGameUk.Application.Dictionary;

public interface IWordDictionaryService
{
    bool ContainsWord(string word);
    bool ContainsSyllable(string word, string syllable);
    IReadOnlyCollection<string> GetCandidates(string syllable);
    string GetRandomSyllable(Random random, SyllableDifficulty difficulty);
}
