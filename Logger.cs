using System;
using System.IO;
using System.Text;

using UnityEngine;

namespace Oxide
{
    /// <summary>
    /// Utility class that assists logging messages
    /// </summary>
    public static class Logger
    {
        /// <summary>
        /// Gets the filename for today's log
        /// </summary>
        private static string Logfile
        {
            get
            {
                string filename = "logs/oxide_" + DateTime.Now.ToString("dd-MM-yyyy") + ".txt";
                return Main.GetPath(filename);
            }
        }

        /// <summary>
        /// Writes the specified message to the log file
        /// </summary>
        /// <param name="message"></param>
        private static void WriteToLogfile(string message)
        {
            string toappend = string.Format("{0}: {1}{2}", DateTime.Now.ToShortTimeString(), message, Environment.NewLine);
            File.AppendAllText(Logfile, toappend);
        }

        /// <summary>
        /// Logs the specified message to console and file
        /// </summary>
        /// <param name="message"></param>
        public static void Message(string message)
        {
            Debug.Log(string.Format("[Oxide] {0}", message));
            WriteToLogfile(message);
        }

        /// <summary>
        /// Logs the specified error to console and file
        /// </summary>
        /// <param name="message"></param>
        public static void Error(string message)
        {
            Debug.LogError(string.Format("[Oxide] {0}", message));
            WriteToLogfile(string.Format("ERROR: {0}", message));
        }

        /// <summary>
        /// Converts an exception to a string
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        private static string ParseError(Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(ex.Source + ": " + ex.ToString());
            sb.AppendLine(ex.StackTrace);
            if (ex.InnerException != null) sb.Append(ParseError(ex.InnerException));
            return sb.ToString();
        }

        /// <summary>
        /// Logs the specified error to console and file
        /// </summary>
        /// <param name="ex"></param>
        public static void Error(Exception ex)
        {
            Error(ParseError(ex));
        }

        /// <summary>
        /// Logs the specified error to console and file
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        public static void Error(string message, Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(message);
            sb.AppendLine(ParseError(ex));
            Error(sb.ToString());
        }

    }
}
