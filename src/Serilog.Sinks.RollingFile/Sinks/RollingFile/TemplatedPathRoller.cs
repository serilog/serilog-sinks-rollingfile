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

using Serilog.Sinks.RollingFile.Sinks.RollingFile;
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
        
        const string DefaultSeparator = "-";

        const string SpecifierMatchGroup = "specifier";
        const string SequenceNumberMatchGroup = "sequence";

        readonly string _pathTemplate;
        readonly Regex _filenameMatcher;
        readonly Specifier _specifier = null;

        public TemplatedPathRoller(string pathTemplate)
        {
            if (pathTemplate == null) throw new ArgumentNullException(nameof(pathTemplate));

            if (pathTemplate.Contains(Specifier.OldStyleDateToken))
                throw new ArgumentException("The old-style date specifier " + Specifier.OldStyleDateToken +
                    " is no longer supported, instead please use " + Specifier.DateToken);

            int numSpecifiers = 0;
            if (pathTemplate.Contains(Specifier.DateToken))
                numSpecifiers++;
            if (pathTemplate.Contains(Specifier.HourToken))
                numSpecifiers++;
            if (pathTemplate.Contains(Specifier.HalfHourToken))
                numSpecifiers++;
            if (numSpecifiers > 1)
                throw new ArgumentException("The date, hour and half-hour specifiers (" +
                    Specifier.DateToken + "," + Specifier.HourToken + "," + Specifier.HalfHourToken +
                    ") cannot be used at the same time");

            var directory = Path.GetDirectoryName(pathTemplate);
            if (string.IsNullOrEmpty(directory))
            {
                directory = Directory.GetCurrentDirectory();
            }

            directory = Path.GetFullPath(directory);

            if (directory.Contains(Specifier.DateToken))
                throw new ArgumentException("The date cannot form part of the directory name");
            if (directory.Contains(Specifier.HourToken))
                throw new ArgumentException("The hour specifiers cannot form part of the directory name");
            if (directory.Contains(Specifier.HalfHourToken))
                throw new ArgumentException("The half-hour specifiers cannot form part of the directory name");

            var filenameTemplate = Path.GetFileName(pathTemplate);
            if (!filenameTemplate.Contains(Specifier.DateToken) && 
                !filenameTemplate.Contains(Specifier.HourToken) &&
                !filenameTemplate.Contains(Specifier.HalfHourToken))
            {
                // If the file name doesn't use any of the admitted specifiers then it is added the date specifier
                // as de default one.
                filenameTemplate = Path.GetFileNameWithoutExtension(filenameTemplate) + DefaultSeparator +
                    Specifier.DateToken + Path.GetExtension(filenameTemplate);
            }

            //---
            // From this point forward we don't reference the Date, Hour or HalfHour concret tokens and formats : 
            // we will reference only the one configured as _specifier.

            _specifier = Specifier.GetFromTemplate(filenameTemplate);

            var indexOfSpecifier = filenameTemplate.IndexOf(_specifier.Token, StringComparison.Ordinal);
            var prefix = filenameTemplate.Substring(0, indexOfSpecifier);
            var suffix = filenameTemplate.Substring(indexOfSpecifier + _specifier.Token.Length);
            _filenameMatcher = new Regex(
                "^" +
                Regex.Escape(prefix) +
                "(?<" + SpecifierMatchGroup + ">\\d{" + _specifier.Format.Length + "})" +
                "(?<" + SequenceNumberMatchGroup + ">_[0-9]{3,}){0,1}" +
                Regex.Escape(suffix) +
                "$");

            DirectorySearchPattern = filenameTemplate.Replace(_specifier.Token, "*");
            LogFileDirectory = directory;
            _pathTemplate = Path.Combine(LogFileDirectory, filenameTemplate);
        }

        public string LogFileDirectory { get; }

        public string DirectorySearchPattern { get; }

        public void GetLogFilePath(DateTime date, int sequenceNumber, out string path)
        {
            DateTime currentCheckpoint = GetCurrentCheckpoint(date);

            var tok = currentCheckpoint.ToString(_specifier.Format, CultureInfo.InvariantCulture);

            if (sequenceNumber != 0)
                tok += "_" + sequenceNumber.ToString("000", CultureInfo.InvariantCulture);

            path = _pathTemplate.Replace(_specifier.Token, tok);
        }

        public IEnumerable<RollingLogFile> SelectMatches(IEnumerable<string> filenames)
        {
            foreach (var filename in filenames)
            {
                var match = _filenameMatcher.Match(filename);
                if (match.Success)
                {
                    var inc = 0;
                    var incGroup = match.Groups[SequenceNumberMatchGroup];
                    if (incGroup.Captures.Count != 0)
                    {
                        var incPart = incGroup.Captures[0].Value.Substring(1);
                        inc = int.Parse(incPart, CultureInfo.InvariantCulture);
                    }

                    DateTime dateTime;
                    var dateTimePart = match.Groups[SpecifierMatchGroup].Captures[0].Value;
                    if (!DateTime.TryParseExact(
                        dateTimePart,
                        _specifier.Format,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out dateTime))
                        continue;

                    yield return new RollingLogFile(filename, dateTime, inc);
                }
            }
        }

        public DateTime GetCurrentCheckpoint(DateTime instant)
        {
            return _specifier.GetCurrentCheckpoint(instant);
        }

        public DateTime GetNextCheckpoint(DateTime instant)
        {
            return _specifier.GetNextCheckpoint(instant);
        }
    }


}