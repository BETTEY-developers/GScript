using System.Text.RegularExpressions;

namespace GScript.Editor;

internal static partial class CodeHighlightRuleSet
{
    
    [GeneratedRegex(@"\d+")]
    private static partial Regex _DigitMatchRegexGenerated();
    public static Regex DigitMatchRegex => _DigitMatchRegexGenerated();
    
    [GeneratedRegex(@"(""{0,1}[^\s]+""{0,1})|(""(.\s)+"")")]
    private static partial Regex _StringMatchRegexGenerated();
    public static Regex StringMatchRegex => _StringMatchRegexGenerated();
    
    [GeneratedRegex(@"('{0,1}[^\s]+'{0,1})|('\s')")]
    private static partial Regex _CharacterMatchRegexGenerated();
    public static Regex CharacterMatchRegex => _CharacterMatchRegexGenerated();
    
    [GeneratedRegex(@"\d+.\d+")]
    private static partial Regex _FloatMatchRegexGenerated(); 
    public static Regex FloatMatchRegex => _FloatMatchRegexGenerated();

    [GeneratedRegex(@"true|false")]
    private static partial Regex _BooleanMatchRegexGenerated();
    public static Regex BooleanMatchRegex => _BooleanMatchRegexGenerated();

    [GeneratedRegex(@".*")]
    private static partial Regex _AnyMatchRegexGenerated();
    public static Regex AnyMatchRegex => _AnyMatchRegexGenerated();

    public static Dictionary<KeyType, Regex> ValueVaildRegularExpressions => new()
    {
        [KeyType.Digit] = DigitMatchRegex,
        [KeyType.Float] = FloatMatchRegex,
        [KeyType.String] = StringMatchRegex,
        [KeyType.Boolean] = BooleanMatchRegex,
        [KeyType.Char] = CharacterMatchRegex,
        [KeyType.Tag] = StringMatchRegex,
        [KeyType.Text] = AnyMatchRegex,
        [KeyType.Property] = AnyMatchRegex
    };

    public static readonly ColorSet _fore = new()
    {
        R = 255,
        G = 255,
        B = 255,
    };

    public static readonly ColorSet _back = new()
    {
        R = 0,
        G = 0,
        B = 0
    };

    public static readonly ColorSet _none = new()
    {
        R = -1,
        G = -1,
        B = -1
    };
}