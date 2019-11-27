namespace System
{
    using System.Globalization;

    /// <summary>
    /// Some useful extensions from https://bitbucket.org/snippets/just_dmitry/LEaRG/datetimeoffset-extensions.
    /// </summary>
    public static class DateTimeOffsetExtensions
    {
        public static int GetQuarter(this DateTimeOffset value)
        {
            return value.Month switch
            {
                1 => 1,
                2 => 1,
                3 => 1,
                4 => 2,
                5 => 2,
                6 => 2,
                7 => 3,
                8 => 3,
                9 => 3,
                10 => 4,
                11 => 4,
                12 => 4,
                _ => 0,
            };
        }

        public static string GetInvertedTicks(this DateTimeOffset value)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:D19}", DateTimeOffset.MaxValue.Ticks - value.UtcTicks);
        }

        public static DateTimeOffset FromInvertedTicks(this string value)
        {
            var ticks = long.Parse(value, CultureInfo.InvariantCulture);
            return new DateTimeOffset(DateTimeOffset.MaxValue.Ticks - ticks, TimeSpan.Zero);
        }

        public static DateTimeOffset Truncate(this DateTimeOffset value, TimeSpan timeSpan)
        {
            if (timeSpan == TimeSpan.Zero)
            {
                return value;
            }

            if (value == DateTimeOffset.MinValue || value == DateTimeOffset.MaxValue)
            {
                return value; // do not modify "guard" values
            }

            return value.AddTicks(-(value.Ticks % timeSpan.Ticks));
        }
    }
}
