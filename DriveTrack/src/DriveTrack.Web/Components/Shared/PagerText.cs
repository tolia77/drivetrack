using System.Globalization;

namespace DriveTrack.Web.Components.Shared;

/// <summary>
/// Which of a list's two nouns follows a count in the pager's range sentence.
/// <para>
/// English needs the singular after exactly one ("of 1 delivery"). Ukrainian puts the whole phrase
/// after "з" in the genitive, and there the noun is singular after every count ending in one except
/// those ending in eleven - "з 21 доставки", "з 11 доставок", "з 86 доставок". Two catalogue entries
/// per list and this rule cover every count; a single noun would be wrong for most of them in one
/// language or the other.
/// </para>
/// </summary>
internal static class PagerText
{
    /// <summary>True when the count takes the list's singular noun in <paramref name="culture"/>.</summary>
    public static bool TakesSingular(int count, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (string.Equals(culture.TwoLetterISOLanguageName, "uk", StringComparison.Ordinal))
        {
            return count % 10 == 1 && count % 100 != 11;
        }

        return count == 1;
    }
}
