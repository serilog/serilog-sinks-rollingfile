using System;

namespace Serilog.Sinks.RollingFile.Sinks.RollingFile
{
    class Specifier
    {
        public const string OldStyleDateToken = "{0}";

        const string DateToken = "{Date}";
        const string HourToken = "{Hour}";
        const string HalfHourToken = "{HalfHour}";
        const string DateFormat = "yyyyMMdd";
        const string HourFormat = "yyyyMMddHH";
        const string HalfHourFormat = "yyyyMMddHHmm";
        static readonly TimeSpan DateInterval = TimeSpan.FromDays(1);
        static readonly TimeSpan HourInterval = TimeSpan.FromHours(1);
        static readonly TimeSpan HalfHourInterval = TimeSpan.FromMinutes(30);

        public static readonly Specifier Date = new Specifier("Date", DateToken, DateFormat, DateInterval);
        public static readonly Specifier Hour = new Specifier("Hour", HourToken, HourFormat, HourInterval);
        public static readonly Specifier HalfHour = new Specifier("HalfHour", HalfHourToken, HalfHourFormat, HalfHourInterval);

        public string Name { get; }
        public string Token { get; }
        public string Format { get; }
        public TimeSpan Interval { get; }

        Specifier(string name, string token, string format, TimeSpan interval)
        {
            Name = name;
            Token = token;
            Format = format;
            Interval = interval;
        }

        public DateTime GetCurrentCheckpoint(DateTime instant)
        {
            if (Token == Hour.Token)
            {
                return instant.Date.AddHours(instant.Hour);
            }
            else if (Token == HalfHour.Token)
            {
                DateTime auxDT = instant.Date.AddHours(instant.Hour);
                if (instant.Minute >= 30)
                    auxDT = auxDT.AddMinutes(30);
                return auxDT;
            }

            return instant.Date;
        }

        public DateTime GetNextCheckpoint(DateTime instant)
        {
            DateTime currentCheckpoint = GetCurrentCheckpoint(instant);
            return currentCheckpoint.Add(Interval);
        }

        public static bool TryGetSpecifier(string template, out Specifier specifier)
        {
            specifier = null;

            if (!string.IsNullOrWhiteSpace(template))
            {
                if (template.Contains(Specifier.Date.Token))
                {
                    specifier = Specifier.Date;
                }
                else if (template.Contains(Specifier.Hour.Token))
                {
                    specifier = Specifier.Hour;
                }
                else if (template.Contains(Specifier.HalfHour.Token))
                {
                    specifier = Specifier.HalfHour;
                }
            }

            return (specifier != null);
        }

    }
}
