namespace WordGameUk.Multiplayer.Core.Multiplayer.Domain;

public interface IWordDictionary
{
    bool ContainsWord(string word);
    bool ContainsSyllable(string word, string syllable);
    string GetRandomSyllable(Random random, SyllableDifficulty difficulty);
}
