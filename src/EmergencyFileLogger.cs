using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ccf.Ck.Libs.Logging
{
    internal class EmergencyFileLogger
    {
        private static readonly string _LogPath;
        private const string ERROR_MESSAGE_TEMPLATE =
        @"/////////////////////////////////////////////////////////////ERROR LOG ENTRY://///////////////////////////////////////////////////////////
        SOURCE: {0}
        LOGLEVEL: {1}
        MESSAGE: {2}
        TIMEUTC: {3}
        ERROR_DETAILS: {4}";

        static EmergencyFileLogger()
        {
            try
            {
                string directory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                _LogPath = Path.Combine(directory, "Logs");
                if (!Directory.Exists(_LogPath))
                {
                    Directory.CreateDirectory(_LogPath);
                }
            }
            catch { }
        }

        public static void Log(string message, Exception exception, LogLevel logLevel)
        {
            if (exception is null)
            {
                exception = new Exception();
            }
            Utilities.ShouldSerializeWithAllDetails = 1;
            Log2File(string.Format(ERROR_MESSAGE_TEMPLATE, exception.Source, logLevel.ToString(), message, DateTime.Now, Utilities.Serialize(logLevel, exception)));
        }

        private static void Log2File(string value)
        {
            try
            {
                StreamWriter w;
                string file = Path.Combine(_LogPath, "Emergency.log");
                if (!File.Exists(file))
                {
                    w = File.CreateText(file);
                }
                else
                {
                    w = File.AppendText(file);
                }

                using (w)
                {
                    w.Write(value);
                }
            }
            catch { }//swallow all exception
        }
    }
}
