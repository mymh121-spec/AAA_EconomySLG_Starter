using System.Collections.Generic;
using System.Globalization;
using Game.Domain.Common;

namespace Game.Domain.Localization
{
    public sealed class LocalizationTable
    {
        private readonly Dictionary<string, string> _korean =
            new Dictionary<string, string>();

        public void Register(string key, string korean)
        {
            if (!string.IsNullOrWhiteSpace(key))
                _korean[key] = korean ?? string.Empty;
        }

        public string Get(string key, string fallback = null)
        {
            if (_korean.TryGetValue(key, out var value))
                return value;

            return fallback ?? key;
        }
    }

    public static class KoreanFormat
    {
        private static readonly CultureInfo Culture =
            CultureInfo.GetCultureInfo("ko-KR");

        public static string Money(decimal value) =>
            value.ToString("N0", Culture) + "원";

        public static string Price(decimal value) =>
            value.ToString("N0", Culture) + "원";

        public static string Quantity(decimal value) =>
            value.ToString("N1", Culture);

        public static string Day(GameDay day) =>
            $"{day.Value}일차";

        public static string Turn(TurnNumber turn) =>
            $"{turn.Value}턴";

        public static string Percent(decimal value) =>
            (value * 100m).ToString("N1", Culture) + "%";
    }
}
