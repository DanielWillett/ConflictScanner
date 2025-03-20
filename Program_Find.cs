using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SDG.Unturned;
using UnturnedAssets;

namespace ConflictScanner;
internal static partial class Program
{
    private static void Find(string input)
    {
        try
        {
            string[] words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            IEnumerable<UnturnedAssetFile> assetsBase = _assets;

            if (words.Length > 0)
            {
                if (words[0].Equals("_", StringComparison.Ordinal))
                {
                    input = string.Join(' ', words, 1, words.Length - 1);
                }
                else
                {
                    Type? type = Type.GetType(words[0], throwOnError: false, ignoreCase: true);
                    if (type == null || !typeof(Asset).IsAssignableFrom(type))
                        type = typeof(Assets).Assembly.GetType(words[0], throwOnError: false, ignoreCase: true);
                    if (type == null || !typeof(Asset).IsAssignableFrom(type))
                        type = Type.GetType("SDG.Unturned." + words[0] + ", Assembly-CSharp", throwOnError: false, ignoreCase: true);
                    if (type == null || !typeof(Asset).IsAssignableFrom(type))
                        type = Assets.assetTypes.getType(ToProperCase(words[0]));
                    if (type == null || !typeof(Asset).IsAssignableFrom(type))
                        type = Type.GetType("SDG.Unturned." + words[0] + "Asset, Assembly-CSharp", throwOnError: false, ignoreCase: true);
                    if (type == null || !typeof(Asset).IsAssignableFrom(type))
                        type = Type.GetType("SDG.Unturned.Item" + words[0] + "Asset, Assembly-CSharp", throwOnError: false, ignoreCase: true);
                    if (type != null)
                    {
                        assetsBase = _assets.Where(x => type.IsAssignableFrom(x.AssetType));
                        input = string.Join(' ', words, 1, words.Length - 1);
                    }
                }
            }

            input = input.Trim();

            List<UnturnedAssetFile> assets = assetsBase
                .OrderBy(x => 
                    x.AssetName.StartsWith(input, StringComparison.InvariantCultureIgnoreCase) || x.FriendlyName != null && x.FriendlyName.StartsWith(input, StringComparison.InvariantCultureIgnoreCase)
                        ? -1
                        : Math.Min(x.FriendlyName != null
                                    ? LevenshteinDistance(x.FriendlyName,
                                        input,
                                        CultureInfo.CurrentCulture,
                                        LevenshteinOptions.AutoComplete | LevenshteinOptions.IgnoreCase |
                                        LevenshteinOptions.IgnorePunctuation | LevenshteinOptions.IgnoreWhitespace)
                                    : int.MaxValue,
                                    LevenshteinDistance(x.AssetName,
                                        input,
                                        CultureInfo.CurrentCulture,
                                        LevenshteinOptions.AutoComplete | LevenshteinOptions.IgnoreCase |
                                        LevenshteinOptions.IgnorePunctuation | LevenshteinOptions.IgnoreWhitespace
                                    )
                                )
                )
                .ThenBy(x => x.AssetName)
                .Take(Math.Max(Console.WindowHeight - 4, 10))
                .ToList();

            if (Guid.TryParse(input, CultureInfo.InvariantCulture, out Guid guid))
            {
                assets.Clear();
                foreach (UnturnedAssetFile file in assetsBase.Where(x => x.Guid == guid))
                {
                    assets.Add(file);
                }
            }

            if (ushort.TryParse(input, CultureInfo.InvariantCulture, out ushort id))
            {
                foreach (UnturnedAssetFile file in assetsBase.Where(x => x.Id == id))
                {
                    assets.Insert(0, file);
                    assets.RemoveAt(assets.Count - 1);
                }
            }

            AssetConsoleMeasurements m = new AssetConsoleMeasurements(assets);
            WriteAssetToConsoleHeader(in m);
            for (int i = assets.Count - 1; i >= 0; i--)
            {
                WriteAssetToConsole(i, assets[i], in m);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }

    private static string ToProperCase(string word)
    {
        if (char.IsUpper(word[0]))
        {
            for (int i = 1; i < word.Length; ++i)
            {
                if (char.IsUpper(word[i]))
                {
                    return word[0] + word.Substring(1).ToLower();
                }
            }

            return word;
        }
        else
        {
            for (int i = 1; i < word.Length; ++i)
            {
                if (char.IsUpper(word[i]))
                {
                    return char.ToUpper(word[0]) + word.Substring(1).ToLower();
                }
            }

            return char.ToUpper(word[0]) + word.Substring(1);
        }
    }

    private const LevenshteinOptions IgnoreAny = (LevenshteinOptions)(1 << 1);

    /// <summary>
    /// Computes the number of edits required to turn one string to another.
    /// </summary>
    /// <remarks>Based on https://en.wikipedia.org/wiki/Levenshtein_distance#Iterative_with_two_matrix_rows.</remarks>
    public static unsafe int LevenshteinDistance(ReadOnlySpan<char> a, ReadOnlySpan<char> b, CultureInfo formatProvider, LevenshteinOptions options = default)
    {
        if (a == b)
            return 0;

        fixed (char* lpA = a)
        fixed (char* lpB = b)
        {
            return LevenshteinDistance(lpA, a.Length, lpB, b.Length, formatProvider, options);
        }
    }

    /// <summary>
    /// Computes the number of edits required to turn one string to another.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <remarks>Based on https://en.wikipedia.org/wiki/Levenshtein_distance#Iterative_with_two_matrix_rows.</remarks>
    public static unsafe int LevenshteinDistance(string a, string b, CultureInfo formatProvider, LevenshteinOptions options = default)
    {
        if (a == null)
            throw new ArgumentNullException(nameof(a));
        if (b == null)
            throw new ArgumentNullException(nameof(b));

        if (ReferenceEquals(a, b) || a.Length == 0 && b.Length == 0)
            return 0;

        fixed (char* lpA = a)
        fixed (char* lpB = b)
        {
            return LevenshteinDistance(lpA, a.Length, lpB, b.Length, formatProvider, options);
        }
    }

    /// <summary>
    /// Computes the number of edits required to turn one string to another.
    /// </summary>
    /// <remarks>Based on https://en.wikipedia.org/wiki/Levenshtein_distance#Iterative_with_two_matrix_rows.</remarks>
    public static unsafe int LevenshteinDistance(char* a, int aChars, char* b, int bChars, CultureInfo formatProvider, LevenshteinOptions options = default)
    {
        if (a == b || aChars == 0 && bChars == 0)
        {
            return 0;
        }

        bool ignoreCase = (options & LevenshteinOptions.IgnoreCase) != 0;
        // remove trailing and leading ignored characters
        if ((options & IgnoreAny) != 0)
        {
            while (aChars > 0 && IsIgnored(a[aChars - 1], options)) { --aChars; }
            while (bChars > 0 && IsIgnored(b[bChars - 1], options)) { --bChars; }

            while (aChars > 0 && IsIgnored(a[0], options)) { ++a; --aChars; }
            while (bChars > 0 && IsIgnored(b[0], options)) { ++b; --bChars; }

            bool canUnIgnoreCase = false;
            if (aChars > 2)
            {
                int ct = 2;
                for (int i = 1; i < aChars - 1; ++i)
                {
                    if (!IsIgnored(a[i], options))
                        ++ct;
                }

                if (ct != aChars)
                {
                    char* newPtr = stackalloc char[ct];
                    int index = -1;
                    for (int i = 0; i < aChars; ++i)
                    {
                        char c = a[i];
                        if (!IsIgnored(c, options))
                        {
                            newPtr[++index] = ignoreCase ? char.ToLower(c, formatProvider) : c;
                        }
                    }

                    a = newPtr;
                    aChars = ct;
                    canUnIgnoreCase = true;
                }
            }

            if (bChars > 2)
            {
                int ct = 2;
                for (int i = 1; i < bChars - 1; ++i)
                {
                    if (!IsIgnored(b[i], options))
                        ++ct;
                }

                if (ct != bChars)
                {
                    char* newPtr = stackalloc char[ct];
                    int index = -1;
                    for (int i = 0; i < bChars; ++i)
                    {
                        char c = b[i];
                        if (!IsIgnored(c, options))
                            newPtr[++index] = ignoreCase ? char.ToLower(c, formatProvider) : c;
                    }

                    b = newPtr;
                    bChars = ct;
                    if (canUnIgnoreCase)
                        ignoreCase = false;
                }
            }
        }

        if (aChars == 0)
            return bChars;
        if (bChars == 0)
            return aChars;

        int* prev = stackalloc int[bChars + 1];
        int* curr = stackalloc int[bChars + 1];

        for (int i = 0; i <= bChars; ++i)
            prev[i] = i;

        bool autocomplete = (options & LevenshteinOptions.AutoComplete) != 0 && aChars > bChars;
        for (int i = 0; i < aChars; ++i)
        {
            curr[0] = i + 1;

            for (int j = 0; j < bChars; ++j)
            {
                int deletionCost = prev[j + 1] + 1;
                int insertionCost = curr[j] + 1;

                bool isDifferent = ignoreCase
                    ? char.ToLower(a[i], formatProvider) != char.ToLower(b[j], formatProvider)
                    : a[i] != b[j];

                int substitutionCost = (isDifferent ? 1 : 0) + prev[j];

                if (autocomplete && j == bChars - 1 && deletionCost < insertionCost && deletionCost < substitutionCost)
                {
                    return prev[bChars];
                }

                curr[j + 1] = Math.Min(Math.Min(deletionCost, insertionCost), substitutionCost);
            }

            int* temp = prev;
            prev = curr;
            curr = temp;
        }

        return prev[bChars];
    }

    private static bool IsIgnored(char c, LevenshteinOptions options)
    {
        if ((options & LevenshteinOptions.IgnoreWhitespace) == LevenshteinOptions.IgnoreWhitespace)
        {
            if (char.IsWhiteSpace(c))
                return true;
        }
        if ((options & LevenshteinOptions.IgnorePunctuation) == LevenshteinOptions.IgnorePunctuation)
        {
            if (char.IsPunctuation(c))
                return true;
        }

        return c == '\0';
    }
}

[Flags]
public enum LevenshteinOptions
{
    /// <summary>
    /// Ignores the casing of the two strings when comparing characters.
    /// </summary>
    IgnoreCase = 1,

    /// <summary>
    /// Ignores any characters that fall under <see cref="char.IsWhiteSpace(char)"/>.
    /// </summary>
    IgnoreWhitespace = (1 << 1) | (1 << 2),

    /// <summary>
    /// Ignores any characters that fall under <see cref="char.IsPunctuation(char)"/>.
    /// </summary>
    IgnorePunctuation = (1 << 1) | (1 << 3),

    /// <summary>
    /// A will be treated as a value being searched for by B. Treats extra letters after B as untyped instead of missing (they don't count towards the distance).
    /// </summary>
    AutoComplete = 1 << 8
}