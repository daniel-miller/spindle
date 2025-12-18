using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Spindle;

/// <summary>
/// Provides methods for string inflection including pluralization, casing, and word transformation
/// </summary>
public static class Inflector
{
    // Cached regex for better performance
    private static readonly Regex CamelCaseSplitRegex = new(
        @"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|(?<=\d)(?=[A-Z])|(?<=[A-Za-z])(?=\d)",
        RegexOptions.Compiled);

    // Common irregular plurals
    private static readonly Dictionary<string, string> IrregularPlurals = new(StringComparer.OrdinalIgnoreCase)
    {
        ["person"] = "people",
        ["child"] = "children",
        ["man"] = "men",
        ["woman"] = "women",
        ["tooth"] = "teeth",
        ["foot"] = "feet",
        ["mouse"] = "mice",
        ["goose"] = "geese"
    };

    // Words that don't change in plural form
    private static readonly HashSet<string> InvariantPlurals = new(StringComparer.OrdinalIgnoreCase)
    {
        "sheep", "fish", "deer", "moose", "series", "species", "money", "rice", "information", "equipment"
    };

    /// <summary>
    /// Converts the first letter of a string to uppercase
    /// </summary>
    /// <param name="input">The string to convert.</param>
    /// <returns>The string with its first letter in uppercase</returns>
    public static string Capitalize(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpperInvariant(input[0]) + input.Substring(1);
    }

    /// <summary>
    /// Converts the first letter of a string to lowercase
    /// </summary>
    /// <param name="input">The string to convert.</param>
    /// <returns>The string with its first letter in lowercase</returns>
    public static string Decapitalize(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToLowerInvariant(input[0]) + input.Substring(1);
    }

    /// <summary>
    /// Pluralizes a singular noun according to English grammar rules
    /// </summary>
    /// <param name="singularNoun">The singular noun to pluralize.</param>
    /// <returns>The pluralized form of the noun</returns>
    public static string Pluralize(string singularNoun)
    {
        if (string.IsNullOrWhiteSpace(singularNoun))
            return singularNoun;

        // Check for invariant plurals
        if (InvariantPlurals.Contains(singularNoun))
            return singularNoun;

        // Check for irregular plurals
        if (TryGetIrregularPlural(singularNoun, out var irregular))
            return irregular!;

        // Apply standard pluralization rules
        return ApplyStandardPluralizationRules(singularNoun);
    }

    /// <summary>
    /// Attempts to get the irregular plural form of a word while preserving its casing
    /// </summary>
    private static bool TryGetIrregularPlural(string word, out string? plural)
    {
        if (IrregularPlurals.TryGetValue(word, out var irregularPlural))
        {
            plural = PreserveCasing(irregularPlural, word);
            return true;
        }

        plural = null;
        return false;
    }

    /// <summary>
    /// Applies standard English pluralization rules to a word
    /// </summary>
    private static string ApplyStandardPluralizationRules(string word)
    {
        // Words ending in 'o' preceded by a consonant
        if (word.EndsWith("o", StringComparison.OrdinalIgnoreCase) &&
            word.Length > 1 &&
            !IsVowel(word[word.Length - 2]))
        {
            return word + "es";
        }

        // Words ending in 'y' preceded by a consonant
        if (word.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
            word.Length > 1 &&
            !IsVowel(word[word.Length - 2]))
        {
            return word.Substring(0, word.Length - 1) + "ies";
        }

        // Words ending in 's', 'ss', 'x', 'z', 'ch', 'sh'
        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("z", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("sh", StringComparison.OrdinalIgnoreCase))
        {
            return word + "es";
        }

        // Words ending in 'f' or 'fe'
        if (word.EndsWith("f", StringComparison.OrdinalIgnoreCase))
        {
            return word.Substring(0, word.Length - 1) + "ves";
        }
        if (word.EndsWith("fe", StringComparison.OrdinalIgnoreCase))
        {
            return word.Substring(0, word.Length - 2) + "ves";
        }

        // Default: add 's'
        return word + "s";
    }

    /// <summary>
    /// Determines if a character is a vowel
    /// </summary>
    private static bool IsVowel(char c)
    {
        return "aeiouAEIOU".IndexOf(c) >= 0;
    }

    /// <summary>
    /// Preserves the casing pattern of a target string when applying it to a source string
    /// </summary>
    /// <param name="source">The source string to transform.</param>
    /// <param name="pattern">The pattern string whose casing to match.</param>
    /// <returns>The source string with casing matching the pattern</returns>
    public static string PreserveCasing(string source, string pattern)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(pattern))
            return source;

        // Handle special cases
        if (pattern.Equals(pattern.ToUpperInvariant()))
            return source.ToUpperInvariant();

        if (pattern.Equals(pattern.ToLowerInvariant()))
            return source.ToLowerInvariant();

        if (char.IsUpper(pattern[0]) && pattern.Length > 1 && pattern.Substring(1).Equals(pattern.Substring(1).ToLowerInvariant()))
            return Capitalize(source.ToLowerInvariant());

        // Match character by character
        var result = new StringBuilder(source.Length);
        for (int i = 0; i < source.Length; i++)
        {
            if (i < pattern.Length)
            {
                result.Append(char.IsUpper(pattern[i])
                    ? char.ToUpperInvariant(source[i])
                    : char.ToLowerInvariant(source[i]));
            }
            else
            {
                // Default to lowercase if pattern is shorter
                result.Append(char.ToLowerInvariant(source[i]));
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Converts a PascalCase or camelCase string to a sentence with spaces
    /// </summary>
    /// <param name="input">The string to convert.</param>
    /// <param name="toLower">If true, converts the result to lowercase.</param>
    /// <returns>The string converted to sentence case</returns>
    public static string ToSentenceCase(string input, bool toLower = true)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var result = CamelCaseSplitRegex.Replace(input, " ");
        return toLower ? result.ToLowerInvariant() : result;
    }

    /// <summary>
    /// Converts a string to title case (each word capitalized)
    /// </summary>
    /// <param name="input">The string to convert.</param>
    /// <returns>The string in title case</returns>
    public static string ToTitleCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLowerInvariant());
    }

    /// <summary>
    /// Converts a string to PascalCase
    /// </summary>
    /// <param name="input">The string to convert.</param>
    /// <returns>The string in PascalCase</returns>
    public static string ToPascalCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var words = input.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.Select(w => Capitalize(w.ToLowerInvariant())));
    }

    /// <summary>
    /// Converts a string to camelCase
    /// </summary>
    /// <param name="input">The string to convert.</param>
    /// <returns>The string in camelCase</returns>
    public static string ToCamelCase(string input)
    {
        var pascalCase = ToPascalCase(input);
        return string.IsNullOrEmpty(pascalCase) ? pascalCase : Decapitalize(pascalCase);
    }

    /// <summary>
    /// Converts a string to snake_case
    /// </summary>
    /// <param name="input">The string to convert.</param>
    /// <returns>The string in snake_case</returns>
    public static string ToSnakeCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sentenceCase = ToSentenceCase(input, false);
        return sentenceCase.Replace(' ', '_').ToLowerInvariant();
    }

    /// <summary>
    /// Converts a string to kebab-case
    /// </summary>
    /// <param name="input">The string to convert.</param>
    /// <returns>The string in kebab-case</returns>
    public static string ToKebabCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sentenceCase = ToSentenceCase(input, false);
        return sentenceCase.Replace(' ', '-').ToLowerInvariant();
    }
}