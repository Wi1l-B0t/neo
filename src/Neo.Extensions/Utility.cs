// Copyright (C) 2015-2025 The Neo Project.
//
// Utility.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Akka.Event;
using Neo.Extensions;
using System.Text;

namespace Neo
{
    public delegate void LogEventHandler(string source, LogLevel level, object message);

    /// <summary>
    /// A utility class that provides common functions.
    /// </summary>
    public static class Utility
    {
        internal class Logger : ReceiveActor
        {
            public Logger()
            {
                Receive<InitializeLogger>(_ => Sender.Tell(new LoggerInitialized()));
                Receive<LogEvent>(e => Log(e.LogSource, (LogLevel)e.LogLevel(), e.Message));
            }
        }

        public static LogLevel LogLevel { get; set; } = LogLevel.Info;

        public static event LogEventHandler? Logging;

        /// <summary>
        /// A strict UTF8 encoding used in NEO system.
        /// </summary>
        public static Encoding StrictUTF8 => StringExtensions.StrictUTF8;

        /// <summary>
        /// Writes a log.
        /// </summary>
        /// <param name="source">The source of the log. Used to identify the producer of the log.</param>
        /// <param name="level">The level of the log.</param>
        /// <param name="message">The message of the log.</param>
        public static void Log(string source, LogLevel level, object message)
        {
            if ((int)level < (int)LogLevel) return;

            Logging?.Invoke(source, level, message);
        }
    }
}
