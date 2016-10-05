using System;

namespace Serilog.Sinks.RollingFile.Sinks.RollingFile
{
    internal class Specifier
    {
        internal const string OldStyleDateToken = "{0}";

        internal const string DateToken = "{Date}";
        internal const string HourToken = "{Hour}";
        internal const string HalfHourToken = "{HalfHour}";

        internal const string DateFormat = "yyyyMMdd";
        internal const string HourFormat = "yyyyMMddHH";
        internal const string HalfHourFormat = "yyyyMMddHHmm";

        internal static readonly TimeSpan DateInterval = TimeSpan.FromDays(1);
        internal static readonly TimeSpan HourInterval = TimeSpan.FromHours(1);
        internal static readonly TimeSpan HalfHourInterval = TimeSpan.FromMinutes(30);

        //-------------------

        public static readonly Specifier Date = new Specifier(SpecifierType.Date);
        public static readonly Specifier Hour = new Specifier(SpecifierType.Hour);
        public static readonly Specifier HalfHour = new Specifier(SpecifierType.HalfHour);

        //-------------------


        public SpecifierType Type { get; }
        public string Name { get; }
        public string Token { get; }
        public string Format { get; }
        public TimeSpan Interval { get; }

        Specifier(SpecifierType type)
        {
            switch (type)
            {
                case SpecifierType.Date:
                    Token = Specifier.DateToken;
                    Format = Specifier.DateFormat;
                    Interval = Specifier.DateInterval;
                    break;

                case SpecifierType.Hour:
                    Token = Specifier.HourToken;
                    Format = Specifier.HourFormat;
                    Interval = Specifier.HourInterval;
                    break;

                case SpecifierType.HalfHour:
                    Token = Specifier.HalfHourToken;
                    Format = Specifier.HalfHourFormat;
                    Interval = Specifier.HalfHourInterval;
                    break;
            }

            Type = type;
            Name = (Token != null ? Token.Replace("{", string.Empty).Replace("}", string.Empty) : Token);
        }

        public DateTime GetCurrentCheckpoint(DateTime instant)
        {
            if (Type == SpecifierType.Hour)
            {
                return instant.Date.AddHours(instant.Hour);
            }
            else if (Type == SpecifierType.HalfHour)
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

        //-------------

        internal static Specifier GetFromTemplate(string template)
        {
            if (!string.IsNullOrWhiteSpace(template))
            {
                if (template.Contains(Specifier.DateToken))
                {
                    return Specifier.Date;
                }
                else if (template.Contains(Specifier.HourToken))
                {
                    return Specifier.Hour;
                }
                else if (template.Contains(Specifier.HalfHourToken))
                {
                    return Specifier.HalfHour;
                }
            }

            return null;
        }

        //-------------

        internal enum SpecifierType
        {
            Date,
            Hour,
            HalfHour
        }
    }
}
