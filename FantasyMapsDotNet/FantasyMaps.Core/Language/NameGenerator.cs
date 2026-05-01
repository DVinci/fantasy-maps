using System.Text;
using System.Text.RegularExpressions;

namespace FantasyMaps.Core.Language;

public static class NameGenerator
{
    private static string Choose(string[] arr)
        => arr[Random.Shared.Next(arr.Length)];

    private static string MakeSyllable(LanguageModel lang)
    {
        string structure = lang.SyllableStructure;
        for (int attempt = 0; attempt < 50; attempt++)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < structure.Length; i++)
            {
                char c = structure[i];
                if (c == '?') continue;
                bool nextIsOpt = (i + 1 < structure.Length && structure[i + 1] == '?');
                if (nextIsOpt && Random.Shared.NextDouble() < 0.5) { i++; continue; }
                sb.Append(c switch {
                    'C' => Choose(lang.Consonants.Length > 0 ? lang.Consonants : ["p"]),
                    'V' => Choose(lang.Vowels.Length > 0 ? lang.Vowels : ["a"]),
                    'S' => Choose(lang.Sibilants.Length > 0 ? lang.Sibilants : lang.Consonants.Length > 0 ? lang.Consonants : ["s"]),
                    'L' => Choose(lang.Liquids.Length > 0 ? lang.Liquids : lang.Consonants.Length > 0 ? lang.Consonants : ["l"]),
                    'F' => Choose(lang.Finals.Length > 0 ? lang.Finals : lang.Consonants.Length > 0 ? lang.Consonants : ["n"]),
                    _ => c.ToString()
                });
            }
            string syll = sb.ToString();
            if (!string.IsNullOrEmpty(lang.Restriction)
                && Regex.IsMatch(syll, lang.Restriction)) continue;
            return ApplyOrthography(lang, syll);
        }
        return "a";
    }

    private static string ApplyOrthography(LanguageModel lang, string syll)
    {
        foreach (var (pattern, replacement) in lang.Orthography)
            syll = syll.Replace(pattern, replacement);
        return syll;
    }

    public static string GetMorpheme(LanguageModel lang, string key)
    {
        if (lang.MorphemeCache.TryGetValue(key, out var cached)) return cached;
        int syllCount = Random.Shared.Next(lang.MinSyllables, lang.MaxSyllables + 1);
        var sb = new StringBuilder();
        for (int i = 0; i < syllCount; i++) sb.Append(MakeSyllable(lang));
        string result = sb.ToString();
        lang.MorphemeCache[key] = result;
        return result;
    }

    public static string MakeName(LanguageModel lang, string key)
    {
        if (lang.WordCache.TryGetValue(key, out var cached)) return cached;

        string name;
        int tries = 0;
        do {
            name = GenerateName(lang, key + tries);
            tries++;
        } while (tries < 100 && (name.Length < 3 || name.Length > 20
            || lang.WordCache.Values.Any(e => e.Contains(name) || name.Contains(e))));

        if (name.Length < 2) name = GetMorpheme(lang, key);
        name = char.ToUpper(name[0]) + name[1..];
        lang.WordCache[key] = name;
        return name;
    }

    private static string GenerateName(LanguageModel lang, string key)
    {
        double r = Random.Shared.NextDouble();
        if (r < 0.5) return GetMorpheme(lang, key);
        if (r < 0.75) return GetMorpheme(lang, key + "a") + lang.Joiner + GetMorpheme(lang, key + "b");
        return "The " + GetMorpheme(lang, key + "a") + " of " + GetMorpheme(lang, key + "b");
    }
}
