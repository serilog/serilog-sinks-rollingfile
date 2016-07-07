# Serilog.Sinks.RollingFile [![Build status](https://ci.appveyor.com/api/projects/status/s9y1u1djdtdwn6u5?svg=true)](https://ci.appveyor.com/project/serilog/serilog-sinks-rollingfile) [![NuGet Version](http://img.shields.io/nuget/v/Serilog.Sinks.RollingFile.svg?style=flat)](https://www.nuget.org/packages/Serilog.Sinks.RollingFile/) [![Documentation](https://img.shields.io/badge/docs-wiki-yellow.svg)](https://github.com/serilog/serilog/wiki) [![Join the chat at https://gitter.im/serilog/serilog](https://img.shields.io/gitter/room/serilog/serilog.svg)](https://gitter.im/serilog/serilog)

Writes [Serilog](https://serilog.net) events to a set of text files, one per day.

### Getting started

Install the [Serilog.Sinks.RollingFile](https://nuget.org/packages/serilog.sinks.rollingfile) package from NuGet:

```powershell
Install-Package Serilog.Sinks.RollingFile
```

To configure the sink in C# code, call `WriteTo.RollingFile()` during logger configuration:

```csharp
var log = new LoggerConfiguration()
    .WriteTo.RollingFile("log-{Date}.txt")
    .CreateLogger();
    
Log.Information("This will be written to the rolling file set");
```

The filename should include the `{Date}` placeholder, which will be replaced with the date of the events contained in the file. Filenames use the `yyyyMMdd` date format so that files can be ordered using a lexicographic sort:

```
log-20160631.txt
log-20160701.txt
log-20160702.txt
```

> **Important:** Only one process may write to a log file at a given time. For multi-process scenarios, either use separate files or [one of the non-file-based sinks](https://github.com/serilog/serilog/wiki/Provided-Sinks).

### Limits

To avoid bringing down apps with runaway disk usage the rolling file sink **limits file size to 1GB by default**. The limit can be changed or removed using the `fileSizeLimitBytes` parameter.

```csharp
    .WriteTo.RollingFile("log-{Date}.txt", fileSizeLimitBytes: null)
```

For the same reason, only **the most recent 31 files** are retained by default (i.e. one long month). To change or remove this limit, pass the `retainedFileCountLimit` parameter.

```csharp
    .WriteTo.RollingFile("log-{Date}.txt", retainedFileCountLimit: null)
```

### XML `<appSettings>` configuration

To use the rolling file sink with the [Serilog.Settings.AppSettings](https://github.com/serilog/serilog-settings-appsettings) package, first install that package if you haven't already done so:

```powershell
Install-Package Serilog.Settings.AppSettings
```

Instead of configuring the logger in code, call `ReadFrom.AppSettings()`:

```csharp
var log = new LoggerConfiguration()
    .ReadFrom.AppSettings()
    .CreateLogger();
```

In your application's `App.config` or `Web.config` file, specify the rolling file sink assembly and required path format under the `<appSettings>` node:

```xml
<configuration>
  <appSettings>
    <add key="serilog:using:RollingFile" value="Serilog.Sinks.RollingFile" />
    <add key="serilog:write-to:RollingFile.pathFormat" value="log-{Date}.txt" />
```

The parameters that can be set through the `serilog:write-to:RollingFile` keys are the method parameters accepted by the `WriteTo.RollingFile()` configuration method. This means, for example, that the `fileSizeLimitBytes` parameter can be set with:

```xml
    <add key="serilog:write-to:RollingFile.fileSizeLimitBytes" value="1234567" />
```

Omitting the `value` will set the parameter to `null`:

```xml
    <add key="serilog:write-to:RollingFile.fileSizeLimitBytes" />
```

In XML and JSON configuration formats, environment variables can be used in setting values. This means, for instance, that the log file path can be based on `TMP` or `APPDATA`:

```xml
    <add key="serilog:write-to:RollingFile.pathFormat" value="%APPDATA%\MyApp\log-{Date}.txt" />
```

### JSON `appsettings.json` configuration

To use the rolling file sink with _Microsoft.Extensions.Configuration_, for example with ASP.NET Core or .NET Core, use the [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration) package. First install that package if you have not already done so:

```powershell
Install-Package Serilog.Settings.Configuration
```

Instead of configuring the rolling file directly in code, call `ReadFrom.Configuration()`:

```csharp
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();
```

In your `appsettings.json` file, under the `Serilog` node, :

```json
{
  "Serilog": {
    "WriteTo": [
      { "Name": "RollingFile", "Args": { "pathFormat": "log-{Date}.txt" } }
    ]
  }
}
```

See the XML `<appSettings>` example above for a discussion of available `Args` options.

### Controlling event formatting

The rolling file sink creates events in a fixed text format by default:

```
2016-07-06 09:02:17.148 +10:00 [Information] HTTP "GET" "/" responded 200 in 1994 ms
```

The format is controlled using an _output template_, which the rolling file configuration method accepts as an `outputTemplate` parameter.

The default format above corresponds to an output template like:

```csharp
    .WriteTo.RollingFile("log-{Date}.txt",
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message}{NewLine}{Exception}")
```

##### JSON event formatting

To write events to the file in an alternative format such as JSON, pass an `ITextFormatter` as the first argument:

```csharp
    .WriteTo.RollingFile(new JsonFormatter(), "log-{Date}.txt")
```

### Alternatives

The default rolling file sink is designed to suit most applications. So that we can keep it maintainable and reliable, it does not provide a large range of optional behavior. Check out alternative implemementations like [this one](https://github.com/BedeGaming/sinks-rollingfile) if your needs aren't met by the default version.

_Copyright &copy; 2016 Serilog Contributors - Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html)._
