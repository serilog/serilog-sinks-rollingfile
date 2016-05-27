# Serilog.Sinks.RollingFile

The rolling file sink for Serilog.

[![Build status](https://ci.appveyor.com/api/projects/status/s9y1u1djdtdwn6u5?svg=true)](https://ci.appveyor.com/project/serilog/serilog-sinks-rollingfile) [![NuGet Version](http://img.shields.io/nuget/v/Serilog.Sinks.RollingFile.svg?style=flat)](https://www.nuget.org/packages/Serilog.Sinks.RollingFile/)

Writes log events to a set of text files, one per day.

The filename can include the `{Date}` placeholder, which will be replaced with the date of the events contained in the file.

```csharp
var log = new LoggerConfiguration()
    .WriteTo.RollingFile("log-{Date}.txt")
    .CreateLogger();
```

To avoid sinking apps with runaway disk usage the rolling file sink **limits file size to 1GB by default**. The limit can be changed or removed using the `fileSizeLimitBytes` parameter.

```csharp
    .WriteTo.RollingFile("log-{Date}.txt", fileSizeLimitBytes: null)
```

For the same reason, only **the most recent 31 files** are retained by default (i.e. one long month). To change or remove this limit, pass the `retainedFileCountLimit` parameter.

```csharp
    .WriteTo.RollingFile("log-{Date}.txt", retainedFileCountLimit: null)
```

> **Important:** Only one process may write to a log file at a given time. For multi-process scenarios, either use separate files or one of the non-file-based sinks.

* [Documentation](https://github.com/serilog/serilog/wiki)

Copyright &copy; 2016 Serilog Contributors - Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html).
