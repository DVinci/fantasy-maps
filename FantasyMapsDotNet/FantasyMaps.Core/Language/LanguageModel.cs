namespace FantasyMaps.Core.Language;

public class LanguageModel
{
    public string[] Consonants { get; set; } = [];
    public string[] Vowels { get; set; } = [];
    public string[] Sibilants { get; set; } = [];
    public string[] Liquids { get; set; } = [];
    public string[] Finals { get; set; } = [];
    public string SyllableStructure { get; set; } = "CVC";
    public string Joiner { get; set; } = " ";
    public int MinSyllables { get; set; } = 1;
    public int MaxSyllables { get; set; } = 2;
    public (string Pattern, string Replacement)[] Orthography { get; set; } = [];
    public string Restriction { get; set; } = "";
    public Dictionary<string, string> WordCache { get; } = [];
    public Dictionary<string, string> MorphemeCache { get; } = [];
}
