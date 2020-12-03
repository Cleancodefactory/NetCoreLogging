using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ccf.Ck.Libs.Logging
{
    internal class EmergencyFileLogger
    {
        private static string _LogPath;
        private const string ERROR_MESSAGE_TEMPLATE =
        @"
        ERROR LOG ENTRY: ///////////////////////////////////////////////////////////////////////////////////////////////////
        SOURCE: {0}
        LOGLEVEL: {1}
        MESSAGE: {2}
        TIMEUTC: {3}
        ERRORXML: {4}";

        static EmergencyFileLogger()
        {
            try
            {
                _LogPath = Path.Combine(System.Reflection.Assembly.GetExecutingAssembly().Location, "Logs");
                if (!Directory.Exists(_LogPath))
                {
                    Directory.CreateDirectory(_LogPath);
                }
            }
            catch { }
        }

        public static void Log(string message, Exception error, LogLevel logLevel)
        {
            if (error is null)
            {
                error = new Exception();
            }
            Log2File(string.Format(ERROR_MESSAGE_TEMPLATE, error.Source, logLevel.ToString(),message, DateTime.Now, FillMetaFromException(error)));
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

        internal static string FillMetaFromException(Exception error)
        {
            ExceptionXElement e = new ExceptionXElement(error);
            return e.ToString();
        }
    }
}
