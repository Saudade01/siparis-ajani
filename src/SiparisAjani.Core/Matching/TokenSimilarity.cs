namespace SiparisAjani.Core.Matching;

/// <summary>Belirteç benzerliği. Rakam içeren belirteçler (ebat, ölçü) yalnızca bire bir eşleşir.</summary>
internal static class TokenSimilarity
{
    private const double PrefixScore = 0.92;
    private const double StemScore = 0.95;
    private const double MinimumFuzzy = 0.80;
    private const int MinimumFuzzyLength = 4;

    public static double Compare(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return 1.0;
        }

        // "4x40" ile "4x50" ya da "8" ile "10" asla benzer sayılmaz: ebat karıştırmak
        // sahada güveni bozan en pahalı hatadır (yanlış eşleşme en pahalı hata).
        if (Text.TurkishTextNormalizer.IsNumeric(left) || Text.TurkishTextNormalizer.IsNumeric(right))
        {
            return 0.0;
        }

        // Türkçe iyelik eki ve ünsüz yumuşaması: "yeleği" ~ "yelek", "ucu" ~ "uç".
        string leftStem = Stem(left);
        string rightStem = Stem(right);
        if (leftStem.Length >= 2 && string.Equals(leftStem, rightStem, StringComparison.Ordinal))
        {
            return StemScore;
        }

        int shorter = Math.Min(left.Length, right.Length);
        if (shorter < MinimumFuzzyLength)
        {
            return 0.0;
        }

        if (left.StartsWith(right, StringComparison.Ordinal) || right.StartsWith(left, StringComparison.Ordinal))
        {
            return PrefixScore;
        }

        int longer = Math.Max(left.Length, right.Length);
        int distance = Distance(left, right, longer - (int)Math.Floor(longer * MinimumFuzzy));
        if (distance < 0)
        {
            return 0.0;
        }

        double ratio = 1.0 - ((double)distance / longer);
        return ratio >= MinimumFuzzy ? ratio : 0.0;
    }

    /// <summary>
    /// Sondaki iyelik ekini atar ve yumuşayan ünsüzü aslına döndürür:
    /// "yeleği" → "yeleg" → "yelek", "ucu" → "uc".
    /// </summary>
    private static string Stem(string token)
    {
        string stem = token;
        if (stem.Length >= 3 && "aeiou".Contains(stem[^1], StringComparison.Ordinal))
        {
            stem = stem[..^1];
        }

        if (stem.Length < 3)
        {
            return stem;
        }

        char last = stem[^1] switch
        {
            'g' => 'k',
            'b' => 'p',
            'd' => 't',
            _ => stem[^1]
        };

        return last == stem[^1] ? stem : string.Concat(stem.AsSpan(0, stem.Length - 1), last.ToString());
    }

    /// <summary>Levenshtein uzaklığı; <paramref name="limit"/> aşılırsa -1 döner.</summary>
    private static int Distance(string left, string right, int limit)
    {
        if (limit < 0)
        {
            return -1;
        }

        if (Math.Abs(left.Length - right.Length) > limit)
        {
            return -1;
        }

        int[] previous = new int[right.Length + 1];
        int[] current = new int[right.Length + 1];
        for (int column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (int row = 1; row <= left.Length; row++)
        {
            current[0] = row;
            int rowBest = current[0];
            for (int column = 1; column <= right.Length; column++)
            {
                int cost = left[row - 1] == right[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + cost);
                rowBest = Math.Min(rowBest, current[column]);
            }

            if (rowBest > limit)
            {
                return -1;
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}