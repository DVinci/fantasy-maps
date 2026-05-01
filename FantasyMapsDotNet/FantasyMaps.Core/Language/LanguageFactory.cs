namespace FantasyMaps.Core.Language;

public static class LanguageFactory
{
    private static readonly string[][] ConsonantSets =
    [
        ["p","t","k","f","s","z","m","n"],
        ["p","t","k","b","d","g","f","s","z","h","m","n","l","r"],
        ["p","t","k","b","d","g","m","n"],
        ["p","k","m","n","h"],
        ["p","t","k","q","b","d","g","m","n","r","l","f","s","x","z"],
        ["p","t","k","b","d","g","f","s","x","m","n","l","r"],
    ];

    private static readonly string[][] VowelSets =
    [
        ["a","e","i","o","u"],
        ["a","e","i","o","u","aa","ee"],
        ["a","e","i","o","u","á","é","í","ó","ú"],
        ["a","e","i","o","u","â","ê","î","ô","û"],
        ["a","e","i","o","u","ä","ö","ü"],
    ];

    private static readonly string[] SyllableStructures =
    [
        "CVC","CVV?C","VC","CVV","CCV","CVVC?","CV","V","CV","CVC",
        "CVC","CVCC","CCVC","CVC?","S?CVC?","S?CV","S?CVC",
        "CVC","S?CVC","CVVC?","CVC","CVC?"
    ];

    private static readonly (string Pattern, string Replacement)[][] OrthographySets =
    [
        [("q","kw"),("c","tsh"),("x","kh"),("ĥ","sh"),("ĝ","ng")],
        [("q","ch"),("c","ts"),("x","kh"),("ĥ","sh"),("ĝ","ng"),("j","y")],
        [("q","qu"),("c","s"),("x","x"),("ĥ","sh"),("ĝ","ng"),("j","j")],
        [("q","qu"),("c","ts"),("x","chs"),("ĥ","sch"),("ĝ","ng"),("j","j")],
    ];

    public static LanguageModel MakeBasicLanguage() => new()
    {
        Consonants = ["p","t","k","m","n"],
        Vowels = ["a","e","i"],
        Sibilants = ["s"],
        Liquids = ["l"],
        Finals = ["n","t"],
        SyllableStructure = "CVC",
        Orthography = [("q","kw")],
        Joiner = " ",
        MinSyllables = 1,
        MaxSyllables = 2
    };

    public static LanguageModel MakeRandomLanguage()
    {
        var r = Random.Shared;
        return new LanguageModel
        {
            Consonants = ConsonantSets[r.Next(ConsonantSets.Length)],
            Vowels = VowelSets[r.Next(VowelSets.Length)],
            Sibilants = ["s","sh","z"],
            Liquids = ["l","r"],
            Finals = ["n","t","s"],
            SyllableStructure = SyllableStructures[r.Next(SyllableStructures.Length)],
            Orthography = OrthographySets[r.Next(OrthographySets.Length)],
            Joiner = r.NextDouble() < 0.5 ? " " : "-",
            MinSyllables = 1,
            MaxSyllables = r.Next(1, 4),
            Restriction = r.NextDouble() < 0.3 ? @"(.)\1" : ""
        };
    }
}
