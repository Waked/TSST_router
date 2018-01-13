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

        // Wraps Console.WriteLine method with style from Colorful.Console and a timestamp
        // from local Stopwatch.
        public static void WriteLine(string format, params object[] values)
        {
            Colorful.Console.WriteLineStyled(style, Timestamp + format, values);
        }

        public static void WriteLine(bool suppressTimestamp, string format, params object[] values)
        {
            if (suppressTimestamp)
                Colorful.Console.WriteLineStyled(style, format, values);
            else
                WriteLine(format, values);
        }

        // Wraps Console.Write method with style from Colorful.Console and a timestamp
        // from local Stopwatch.
        public static void Write(string format, params object[] values)
        {
            Colorful.Console.WriteStyled(style, Timestamp + format, values);
        }

        public static void PrintAsciiTitle(string value)
        {
            Colorful.Console.WriteAscii(value, Color.CornflowerBlue);
        }

        public static void ResetTimer()
        {
            timer.Restart();
        }

    }
}
