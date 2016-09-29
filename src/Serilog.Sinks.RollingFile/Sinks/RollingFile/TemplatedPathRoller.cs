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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Serilog.Sinks.RollingFile
{
    // Rolls files based on the current date, using a path
    // formatting pattern like:
    //    Logs/log-{Date}.txt
    //
    class TemplatedPathRoller
    {
        const string OldStyleDateSpecifier = "{0}";
        const string DateSpecifier = "{Date}";
        const string DateFormat = "yyyyMMdd";
        const string HourSpecifier = "{Hour}";
        const string HourFormat = "yyyyMMddHH";
        const string HalfHourSpecifier = "{HalfHour}";
        const string HalfHourFormat = "yyyyMMddHHmm";
        const string DefaultSeparator = "-";

        const string MatcherMarkSpecifier = "date";
        const string MatcherMarkInc = "inc";

        readonly string _pathTemplate;
        readonly Regex _filenameMatcher;
        readonly SpecifierTypeEnum _specifierType = SpecifierTypeEnum.None;
        // Concret used Date or Hour specifier.
        readonly string _usedSpecifier = string.Empty;
        readonly string _usedFormat = string.Empty;

        public TemplatedPathRoller(string pathTemplate)
        {
            if (pathTemplate == null) throw new ArgumentNullException(nameof(pathTemplate));

            if (pathTemplate.Contains(OldStyleDateSpecifier))
                throw new ArgumentException("The old-style date specifier " + OldStyleDateSpecifier +
                    " is no longer supported, instead please use " + DateSpecifier);

            int numSpecifiers = 0;
            if (pathTemplate.Contains(DateSpecifier))
                numSpecifiers++;
            if (pathTemplate.Contains(HourSpecifier))
                numSpecifiers++;
            if (pathTemplate.Contains(HalfHourSpecifier))
                numSpecifiers++;
            if (numSpecifiers > 1)
                throw new ArgumentException("The date, hour and half-hour specifiers (" + 
                    DateSpecifier + "," + HourSpecifier + "," + HalfHourSpecifier +
                    ") cannot be used at the same time");

            var directory = Path.GetDirectoryName(pathTemplate);
            if (string.IsNullOrEmpty(directory))
            {
                directory = Directory.GetCurrentDirectory();
            }

            directory = Path.GetFullPath(directory);

            if (directory.Contains(DateSpecifier))
                throw new ArgumentException("The date cannot form part of the directory name");
            if (directory.Contains(HourSpecifier))
                throw new ArgumentException("The hour specifiers cannot form part of the directory name");
            if (directory.Contains(HalfHourSpecifier))
                throw new ArgumentException("The half-hour specifiers cannot form part of the directory name");

            var filenameTemplate = Path.GetFileName(pathTemplate);
            if (!filenameTemplate.Contains(DateSpecifier) && 
                !filenameTemplate.Contains(HourSpecifier) &&
                !filenameTemplate.Contains(HalfHourSpecifier))
            {
                // If the file name doesn't use any of the admitted specifiers then it is added the date specifier
                // as de default one.
                filenameTemplate = Path.GetFileNameWithoutExtension(filenameTemplate) + DefaultSeparator +
                    DateSpecifier + Path.GetExtension(filenameTemplate);
            }

            //---
            // From this point forward we don't reference the Date or Hour concret specifiers and formats : 
            // we will reference only the one set as "used" (_usedSpecifier and _usedFormat).

            if (filenameTemplate.Contains(DateSpecifier))
            {
                _usedSpecifier = DateSpecifier;
                _usedFormat = DateFormat;
                _specifierType = SpecifierTypeEnum.Date;
            }
            else if (filenameTemplate.Contains(HourSpecifier))
            {
                _usedSpecifier = HourSpecifier;
                _usedFormat = HourFormat;
                _specifierType = SpecifierTypeEnum.Hour;
            }
            else if (filenameTemplate.Contains(HalfHourSpecifier))
            {
                _usedSpecifier = HalfHourSpecifier;
                _usedFormat = HalfHourFormat;
                _specifierType = SpecifierTypeEnum.HalfHour;
            }

            var indexOfSpecifier = filenameTemplate.IndexOf(_usedSpecifier, StringComparison.Ordinal);
            var prefix = filenameTemplate.Substring(0, indexOfSpecifier);
            var suffix = filenameTemplate.Substring(indexOfSpecifier + _usedSpecifier.Length);
            _filenameMatcher = new Regex(
                "^" +
                Regex.Escape(prefix) +
                "(?<" + MatcherMarkSpecifier + ">\\d{" + _usedFormat.Length + "})" +
                "(?<" + MatcherMarkInc + ">_[0-9]{3,}){0,1}" +
                Regex.Escape(suffix) +
                "$");

            DirectorySearchPattern = filenameTemplate.Replace(_usedSpecifier, "*");
            LogFileDirectory = directory;
            _pathTemplate = Path.Combine(LogFileDirectory, filenameTemplate);
        }

        public string LogFileDirectory { get; }

        public string DirectorySearchPattern { get; }

        public void GetLogFilePath(DateTime date, int sequenceNumber, out string path)
        {
            DateTime currentCheckpoint = GetCurrentCheckpoint(date);

            var tok = currentCheckpoint.ToString(_usedFormat, CultureInfo.InvariantCulture);

            if (sequenceNumber != 0)
                tok += "_" + sequenceNumber.ToString("000", CultureInfo.InvariantCulture);

            path = _pathTemplate.Replace(_usedSpecifier, tok);
        }

        public IEnumerable<RollingLogFile> SelectMatches(IEnumerable<string> filenames)
        {
            foreach (var filename in filenames)
            {
                var match = _filenameMatcher.Match(filename);
                if (match.Success)
                {
                    var inc = 0;
                    var incGroup = match.Groups[MatcherMarkInc];
                    if (incGroup.Captures.Count != 0)
                    {
                        var incPart = incGroup.Captures[0].Value.Substring(1);
                        inc = int.Parse(incPart, CultureInfo.InvariantCulture);
                    }

                    DateTime dateTime;
                    var dateTimePart = match.Groups[MatcherMarkSpecifier].Captures[0].Value;
                    if (!DateTime.TryParseExact(
                        dateTimePart,
                        _usedFormat,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out dateTime))
                        continue;

                    yield return new RollingLogFile(filename, dateTime, inc);
                }
            }
        }

        public DateTime GetCurrentCheckpoint(DateTime date)
        {
            if (_specifierType == SpecifierTypeEnum.Hour)
            {
                return date.Date.AddHours(date.Hour);
            }
            else if (_specifierType == SpecifierTypeEnum.HalfHour)
            {
                DateTime auxDT = date.Date.AddHours(date.Hour);
                if (date.Minute >= 30)
                    auxDT = auxDT.AddMinutes(30);
                return auxDT;
            }

            return date.Date;
        }

        public DateTime GetNextCheckpoint(DateTime date)
        {
            DateTime currentCheckpoint = GetCurrentCheckpoint(date);

            if (_specifierType == SpecifierTypeEnum.Hour)
            {
                return currentCheckpoint.AddHours(1);
            }
            else if (_specifierType == SpecifierTypeEnum.HalfHour)
            {
                return currentCheckpoint.AddMinutes(30);
            }

            return currentCheckpoint.AddDays(1);
        }
    }

    enum SpecifierTypeEnum
    {
        None,
        Date,
        Hour,
        HalfHour
    }

}