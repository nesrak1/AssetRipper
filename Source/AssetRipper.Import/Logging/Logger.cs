﻿using AssetRipper.Import.Utils;
using System.Globalization;
using System.Text;

namespace AssetRipper.Import.Logging
{
	public static class Logger
	{
		private static readonly object _lock = new();
		private static readonly List<ILogger> loggers = new();
		public static bool AllowVerbose { get; set; }

		public static event Action<string, object?> OnStatusChanged = (_, _) => { };

		static Logger()
		{
			Cpp2IL.Core.Logging.Logger.InfoLog += (message, source) => LogCpp2IL(LogType.Info, message);
			Cpp2IL.Core.Logging.Logger.WarningLog += (message, source) => LogCpp2IL(LogType.Verbose, message);
			Cpp2IL.Core.Logging.Logger.ErrorLog += (message, source) => LogCpp2IL(LogType.Error, message);
			Cpp2IL.Core.Logging.Logger.VerboseLog += (message, source) => LogCpp2IL(LogType.Verbose, message);
		}

		private static void LogCpp2IL(LogType logType, string message)
		{
			Log(logType, LogCategory.Cpp2IL, message.Trim());
		}

		public static void Log(LogType type, LogCategory category, string message)
		{
#if !DEBUG
			if (type == LogType.Debug)
			{
				return;
			}
#endif
			if (type == LogType.Verbose && !AllowVerbose)
			{
				return;
			}

			if (message == null)
			{
				throw new ArgumentNullException(nameof(message));
			}

			lock (_lock)
			{
				foreach (ILogger instance in loggers)
				{
					instance?.Log(type, category, message);
				}
			}
		}

		public static void Log(LogType type, LogCategory category, string[] messages)
		{
			if (messages == null)
			{
				throw new ArgumentNullException(nameof(messages));
			}

			foreach (string message in messages)
			{
				Log(type, category, message);
			}
		}

		public static void BlankLine() => BlankLine(1);
		public static void BlankLine(int numLines)
		{
			foreach (ILogger instance in loggers)
			{
				instance?.BlankLine(numLines);
			}
		}

		public static void Info(string message) => Log(LogType.Info, LogCategory.None, message);
		public static void Info(LogCategory category, string message) => Log(LogType.Info, category, message);
		public static void Warning(string message) => Log(LogType.Warning, LogCategory.None, message);
		public static void Warning(LogCategory category, string message) => Log(LogType.Warning, category, message);
		public static void Error(string message) => Log(LogType.Error, LogCategory.None, message);
		public static void Error(LogCategory category, string message) => Log(LogType.Error, category, message);
		public static void Error(Exception e) => Error(LogCategory.None, null, e);
		public static void Error(string message, Exception e) => Error(LogCategory.None, message, e);
		public static void Error(LogCategory category, string? message, Exception e)
		{
			StringBuilder sb = new();
			if (message != null)
			{
				sb.AppendLine(message);
			}

			sb.AppendLine(e.ToString());
			Log(LogType.Error, category, sb.ToString());
		}
		public static void Verbose(string message) => Log(LogType.Verbose, LogCategory.None, message);
		public static void Verbose(LogCategory category, string message) => Log(LogType.Verbose, category, message);
		public static void Debug(string message) => Log(LogType.Debug, LogCategory.None, message);
		public static void Debug(LogCategory category, string message) => Log(LogType.Debug, category, message);

		private static void LogReleaseInformation()
		{
#if DEBUG
			Log(LogType.Info, LogCategory.System, $"AssetRipper Build Type: Debug {GetBuildType()}");
#else
			Log(LogType.Info, LogCategory.System, $"AssetRipper Build Type: Release {GetBuildType()}");
#endif
		}

		private static string GetBuildType()
		{
			return File.Exists(ExecutingDirectory.Combine("AssetRipper.Assets.dll")) ? "Compiled" : "Published";
		}

		private static void LogOperatingSystemInformation()
		{
			Log(LogType.Info, LogCategory.System, $"System Version: {Environment.OSVersion.VersionString}");
			string architecture = Environment.Is64BitOperatingSystem ? "64 bit" : "32 bit";
			Log(LogType.Info, LogCategory.System, $"Operating System: {GetOsName()} {architecture}");
		}

		private static void ErrorIfBigEndian()
		{
			if (!BitConverter.IsLittleEndian)
			{
				Error("Big Endian processors are not supported!");
			}
		}

		public static void LogSystemInformation(string programName)
		{
			Log(LogType.Info, LogCategory.System, programName);
			LogOperatingSystemInformation();
			ErrorIfBigEndian();
			Log(LogType.Info, LogCategory.System, $"AssetRipper Version: {typeof(Logger).Assembly.GetName().Version}");
			LogReleaseInformation();
			Log(LogType.Info, LogCategory.System, $"UTC Current Time: {GetCurrentTime()}");
			Log(LogType.Info, LogCategory.System, $"UTC Compile Time: {GetCompileTime()}");
		}

		/// <summary>
		/// Get the current time.
		/// </summary>
		/// <remarks>
		/// This format matches the format used in <see cref="GetCompileTime"/>
		/// </remarks>
		/// <returns>A string like "Thu Nov 24 18:39:37 UTC 2022"</returns>
		private static string GetCurrentTime()
		{
			DateTime now = DateTime.UtcNow;
			StringBuilder sb = new();
			sb.Append(now.DayOfWeek switch
			{
				DayOfWeek.Sunday => "Sun",
				DayOfWeek.Monday => "Mon",
				DayOfWeek.Tuesday => "Tue",
				DayOfWeek.Wednesday => "Wed",
				DayOfWeek.Thursday => "Thu",
				DayOfWeek.Friday => "Fri",
				DayOfWeek.Saturday => "Sat",
				_ => throw new NotSupportedException(),
			});
			sb.Append(' ');
			sb.Append(now.Month switch
			{
				1 => "Jan",
				2 => "Feb",
				3 => "Mar",
				4 => "Apr",
				5 => "May",
				6 => "Jun",
				7 => "Jul",
				8 => "Aug",
				9 => "Sep",
				10 => "Oct",
				11 => "Nov",
				12 => "Dec",
				_ => throw new NotSupportedException(),
			});
			sb.Append(' ');
			sb.Append($"{now.Day,2}");
			sb.Append(' ');
			sb.Append(now.TimeOfDay.Hours.ToString("00", CultureInfo.InvariantCulture));
			sb.Append(':');
			sb.Append(now.TimeOfDay.Minutes.ToString("00", CultureInfo.InvariantCulture));
			sb.Append(':');
			sb.Append(now.TimeOfDay.Seconds.ToString("00", CultureInfo.InvariantCulture));
			sb.Append(" UTC ");
			sb.Append(now.Year);
			return sb.ToString();
		}

		private static string GetCompileTime()
		{
			string path = ExecutingDirectory.Combine("compile_time.txt");
			if (File.Exists(path))
			{
				return File.ReadAllText(path).Trim();
			}
			else
			{
				return "Unknown";
			}
		}

		private static string GetOsName()
		{
			if (OperatingSystem.IsWindows())
			{
				return "Windows";
			}
			else if (OperatingSystem.IsLinux())
			{
				return "Linux";
			}
			else if (OperatingSystem.IsMacOS())
			{
				return "MacOS";
			}
			else if (OperatingSystem.IsBrowser())
			{
				return "Browser";
			}
			else if (OperatingSystem.IsAndroid())
			{
				return "Android";
			}
			else if (OperatingSystem.IsIOS())
			{
				return "iOS";
			}
			else if (OperatingSystem.IsFreeBSD())
			{
				return "FreeBSD";
			}
			else
			{
				return "Other";
			}
		}

		public static void Add(ILogger logger) => loggers.Add(logger);

		public static void Remove(ILogger logger) => loggers.Remove(logger);

		public static void Clear() => loggers.Clear();

		public static void SendStatusChange(string newStatus, object? context = null) => OnStatusChanged(newStatus, context);
	}
}
