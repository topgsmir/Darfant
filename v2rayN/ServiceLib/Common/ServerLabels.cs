using System.Globalization;
using System.Text.RegularExpressions;

namespace ServiceLib.Common;

public static class ServerLabels
{
    private static readonly Dictionary<string, string> CountryNames = BuildCountryNames();
    private static readonly HashSet<string> CountryCodes = CountryNames.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex CodePattern = new(@"(?<![A-Za-z])([A-Z]{2})(?![A-Za-z])", RegexOptions.Compiled);

    public static EServerOperator DetectOperator(string? remarks, string? subscriptionName)
    {
        var text = Normalize($"{remarks} {subscriptionName}");
        var irancell = Regex.IsMatch(text, @"ایرانسل|(?<![a-z])(?:irancell|mtn)(?![a-z])", RegexOptions.IgnoreCase);
        var hamrahAval = Regex.IsMatch(text, @"همراه\s*اول|(?<![a-z])(?:mci|hamrah[\s_-]*aval|irmci)(?![a-z])", RegexOptions.IgnoreCase);
        if ((irancell && hamrahAval) || Regex.IsMatch(text,
            @"(?:همه|تمام)\s*(?:اپراتور|اپراتورها)|(?<![a-z])all[\s_-]*(?:operators?|isp)(?![a-z])",
            RegexOptions.IgnoreCase))
        {
            return EServerOperator.AllOperators;
        }
        if (irancell)
        {
            return EServerOperator.Irancell;
        }
        return hamrahAval ? EServerOperator.HamrahAval : EServerOperator.General;
    }

    public static string ProfileKey(ProfileItemModel profile)
    {
        var identity = JsonSerializer.Serialize(new[]
        {
            profile.Subid ?? "", profile.Address?.ToLowerInvariant() ?? "",
            profile.Port.ToString(CultureInfo.InvariantCulture), profile.ConfigType.ToString(), profile.Remarks ?? ""
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    public static EServerOperator GetOperator(ProfileItemModel profile, Dictionary<string, EServerOperator> overrides)
    {
        return overrides.TryGetValue(ProfileKey(profile), out var value) && Enum.IsDefined(value)
            ? value : DetectOperator(profile.Remarks, profile.SubRemarks);
    }

    public static bool MatchesOperator(EServerOperator category, EServerOperator serverOperator) =>
        category == EServerOperator.General || serverOperator == category
        || (serverOperator == EServerOperator.AllOperators && category is EServerOperator.Irancell or EServerOperator.HamrahAval);

    public static string? CountryCode(string? remarks, string? ipInfo)
    {
        // Measured exit metadata takes priority over a subscription's display name.
        var measured = FindCountry(ipInfo);
        return measured ?? FindCountry(remarks);
    }

    private static string? FindCountry(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var runes = value.EnumerateRunes().ToArray();
        for (var index = 0; index + 1 < runes.Length; index++)
        {
            if (runes[index].Value is >= 0x1F1E6 and <= 0x1F1FF && runes[index + 1].Value is >= 0x1F1E6 and <= 0x1F1FF)
            {
                var code = new string([(char)('A' + runes[index].Value - 0x1F1E6), (char)('A' + runes[index + 1].Value - 0x1F1E6)]);
                if (CountryCodes.Contains(code))
                {
                    return code;
                }
            }
        }
        var normalized = Normalize(value);
        foreach (var country in CountryNames.OrderByDescending(pair => pair.Key.Length))
        {
            var start = normalized.IndexOf(country.Key, StringComparison.OrdinalIgnoreCase);
            if (start >= 0 && (start == 0 || !char.IsLetter(normalized[start - 1]))
                && (start + country.Key.Length == normalized.Length || !char.IsLetter(normalized[start + country.Key.Length])))
            {
                return country.Value;
            }
        }
        foreach (Match match in CodePattern.Matches(value))
        {
            var code = match.Groups[1].Value.ToUpperInvariant();
            if (code == "UK")
            {
                return "GB";
            }
            if (CountryCodes.Contains(code))
            {
                return code;
            }
        }
        return null;
    }

    private static string Normalize(string text) => text.Replace('ي', 'ی').Replace('ك', 'ک').Replace('\u200c', ' ');

    private static Dictionary<string, string> BuildCountryNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                if (region.TwoLetterISORegionName.Length == 2)
                {
                    names.TryAdd(region.EnglishName, region.TwoLetterISORegionName);
                }
            }
            catch (ArgumentException)
            {
                // Some supplemental cultures do not have a geographic region.
            }
        }
        foreach (var item in new[]
        {
            ("آلمان", "DE"), ("هلند", "NL"), ("فرانسه", "FR"), ("فنلاند", "FI"), ("انگلستان", "GB"),
            ("بریتانیا", "GB"), ("آمریکا", "US"), ("امریکا", "US"), ("کانادا", "CA"), ("ترکیه", "TR"),
            ("ایران", "IR"), ("سوئد", "SE"), ("سوئیس", "CH"), ("نروژ", "NO"),
            ("دانمارک", "DK"), ("لهستان", "PL"), ("روسیه", "RU"), ("اسپانیا", "ES"), ("ایتالیا", "IT"),
            ("اتریش", "AT"), ("امارات", "AE"), ("سنگاپور", "SG"), ("ژاپن", "JP"), ("هند", "IN"),
            ("هنگ کنگ", "HK"), ("Hong Kong", "HK"), ("Taiwan", "TW"), ("تایوان", "TW"),
            ("استرالیا", "AU"), ("چین", "CN"), ("کره جنوبی", "KR"), ("United States", "US"),
            ("United Kingdom", "GB"), ("South Korea", "KR"), ("Russia", "RU"), ("Turkey", "TR"),
            ("Deutschland", "DE"), ("Netherlands", "NL"), ("Singapore", "SG")
        })
        {
            names[item.Item1] = item.Item2;
        }
        return names;
    }
}
