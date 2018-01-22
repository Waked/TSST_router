using Colorful;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSSTRouter
{
    static class Log
    {
        // Colorful.Console stylesheet used to style console output
        private static readonly StyleSheet style;

        // This will be used for logging purposes
        private static Stopwatch timer;

        // Buffer to store lines during pause
        private static List<string> logBuffer;

        // Pause flag
        private static bool isPaused = false;

        // Static constructor - initializes certain static objects
        // and values.
        static Log()
        {
            // Prepare console styling
            style = new StyleSheet(Color.LightGray);
            style.AddStyle("FWD", Color.CornflowerBlue);
            style.AddStyle("RX", Color.DeepPink);
            style.AddStyle("TX", Color.LawnGreen);
            style.AddStyle("MGMT", Color.Orange);
            style.AddStyle("ERROR", Color.Red);
            style.AddStyle("DROP", Color.OrangeRed);
            style.AddStyle("LRM", Color.DeepPink);

            logBuffer = new List<string>();

            timer = new Stopwatch();
            timer.Start();
        }
        public static string Timestamp
        {
            get
            {
                return String.Format("{0:00}:{1:00}.{2:000} ", timer.Elapsed.Minutes, timer.Elapsed.Seconds, timer.Elapsed.Milliseconds);
            }
        }

        public static bool IsPaused { get => isPaused; private set => isPaused = value; }

        // Wraps Console.WriteLine method with style from Colorful.Console and a timestamp
        // from local Stopwatch.
        public static void WriteLine(string format, params object[] values)
        {
            WriteLine(false, format, values);
        }

        public static void WriteLine(bool suppressTimestamp, string format, params object[] values)
        {
            string str;

            if (suppressTimestamp)
                str = String.Format(format, values);
            else
                str = String.Format(Timestamp + format, values);

            if (IsPaused)
                logBuffer.Add(str + '\n');
            else
                Colorful.Console.WriteLineStyled(style, str);
        }

        // Wraps Console.Write method with style from Colorful.Console and a timestamp
        // from local Stopwatch.
        public static void Write(string format, params object[] values)
        {
            string str = String.Format(Timestamp + format, values);
            if (IsPaused)
                logBuffer.Add(str);
            else
                Colorful.Console.WriteStyled(style, str);
        }

        public static void PrintAsciiTitle(string value)
        {
            Colorful.Console.WriteAscii(value, Color.CornflowerBlue);
        }

        public static void ResetTimer()
        {
            timer.Restart();
        }

        public static void Pause()
        {
            IsPaused = true;
            Colorful.Console.WriteLineStyled(style, "#### PAUSED ####");
        }

        public static void Unpause()
        {
            Colorful.Console.WriteLineStyled(style, "#### UNPAUSED ####");
            IsPaused = false;
            foreach (string str in logBuffer)
            {
                Colorful.Console.WriteStyled(style, str);
            }
        }
    }
}
