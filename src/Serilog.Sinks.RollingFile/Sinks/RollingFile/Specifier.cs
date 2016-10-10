// Copyright 2013-2016 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Linq;

namespace Serilog.Sinks.RollingFile
{
    class Specifier
    {
        public static readonly Specifier Date = new Specifier("Date", "yyyyMMdd", TimeSpan.FromDays(1));
        public static readonly Specifier Hour = new Specifier("Hour", "yyyyMMddHH", TimeSpan.FromHours(1));
        public static readonly Specifier HalfHour = new Specifier("HalfHour", "yyyyMMddHHmm", TimeSpan.FromMinutes(30));

        public string Token { get; }
        public string Format { get; }
        public TimeSpan Interval { get; }

        Specifier(string name, string format, TimeSpan interval)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (format == null) throw new ArgumentNullException(nameof(format));

            Token = "{" + name + "}";
            Format = format;
            Interval = interval;
        }

        public DateTime GetCurrentCheckpoint(DateTime instant)
        {
            if (this == Hour)
            {
                return instant.Date.AddHours(instant.Hour);
            }

            if (this == HalfHour)
            {
                var hour = instant.Date.AddHours(instant.Hour);
                if (instant.Minute >= 30)
                    return hour.AddMinutes(30);
                return hour;
            }

            return instant.Date;
        }

        public DateTime GetNextCheckpoint(DateTime instant) => GetCurrentCheckpoint(instant).Add(Interval);

        public static bool TryGetSpecifier(string pathTemplate, out Specifier specifier)
        {
            if (pathTemplate == null) throw new ArgumentNullException(nameof(pathTemplate));

            var specifiers = new[] { HalfHour, Hour, Date }.Where(s => pathTemplate.Contains(s.Token)).ToArray();
            
            if (specifiers.Length > 1)
                throw new ArgumentException("Only one interval specifier can be used in a rolling log file path.", nameof(pathTemplate));

            specifier = specifiers.FirstOrDefault();
            return specifier != null;
        }
    }
}
