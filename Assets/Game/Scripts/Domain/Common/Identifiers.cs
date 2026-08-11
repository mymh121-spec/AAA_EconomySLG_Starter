using System;

namespace Game.Domain.Common
{
    public readonly struct ResourceId : IEquatable<ResourceId>
    {
        public string Value { get; }

        public ResourceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Resource ID cannot be empty.", nameof(value));

            Value = value.Trim().ToLowerInvariant();
        }

        public bool Equals(ResourceId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ResourceId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value;
        public static implicit operator ResourceId(string value) => new ResourceId(value);
    }

    public readonly struct CompanyId : IEquatable<CompanyId>
    {
        public string Value { get; }
        public CompanyId(string value) => Value = value;
        public bool Equals(CompanyId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CompanyId other && Equals(other);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public override string ToString() => Value;
    }

    public readonly struct RegionId : IEquatable<RegionId>
    {
        public string Value { get; }
        public RegionId(string value) => Value = value;
        public bool Equals(RegionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RegionId other && Equals(other);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public override string ToString() => Value;
    }

    public readonly struct WarehouseId : IEquatable<WarehouseId>
    {
        public string Value { get; }
        public WarehouseId(string value) => Value = value;
        public bool Equals(WarehouseId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is WarehouseId other && Equals(other);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public override string ToString() => Value;
    }

    public readonly struct FactoryId : IEquatable<FactoryId>
    {
        public string Value { get; }
        public FactoryId(string value) => Value = value;
        public bool Equals(FactoryId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is FactoryId other && Equals(other);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public override string ToString() => Value;
    }

    public readonly struct GameDay : IEquatable<GameDay>
    {
        public int Value { get; }
        public GameDay(int value) => Value = Math.Max(0, value);
        public GameDay Next() => new GameDay(Value + 1);
        public GameDay Add(int days) => new GameDay(Value + days);
        public bool Equals(GameDay other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GameDay other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"{Value}일차";
    }

    public readonly struct GameCalendarDate : IEquatable<GameCalendarDate>
    {
        public const int MonthsPerYear = 12;
        public const int DaysPerMonth = 30;
        public const int DaysPerYear = MonthsPerYear * DaysPerMonth;

        public int Year { get; }
        public int Month { get; }
        public int Day { get; }

        public GameCalendarDate(int year, int month, int day)
        {
            Year = Math.Max(1, year);
            Month = Math.Clamp(month, 1, MonthsPerYear);
            Day = Math.Clamp(day, 1, DaysPerMonth);
        }

        public static GameCalendarDate FromDayNumber(int dayNumber)
        {
            int zeroBasedDay = Math.Max(1, dayNumber) - 1;
            int year = zeroBasedDay / DaysPerYear + 1;
            int dayOfYear = zeroBasedDay % DaysPerYear;
            int month = dayOfYear / DaysPerMonth + 1;
            int day = dayOfYear % DaysPerMonth + 1;
            return new GameCalendarDate(year, month, day);
        }

        public bool Equals(GameCalendarDate other)
        {
            return Year == other.Year &&
                   Month == other.Month &&
                   Day == other.Day;
        }

        public override bool Equals(object obj)
        {
            return obj is GameCalendarDate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Year * 397) ^ Month) * 397 ^ Day;
            }
        }

        public override string ToString()
        {
            return Year == 1
                ? $"{Month}월 {Day}일"
                : $"{Year}년 {Month}월 {Day}일";
        }
    }

    public readonly struct TurnNumber : IEquatable<TurnNumber>
    {
        public int Value { get; }

        public TurnNumber(int value)
        {
            Value = Math.Max(1, value);
        }

        public TurnNumber Next() => new TurnNumber(Value + 1);
        public bool Equals(TurnNumber other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TurnNumber other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"{Value}턴";
    }
}
