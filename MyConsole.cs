using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AmazingDuinoInterface
{
    public static class MyConsole
    {
        private static Dictionary<int, ConsoleColor> _foregroundChanges = new Dictionary<int, ConsoleColor>();
        private static Dictionary<int, ConsoleColor> _backgroundChanges = new Dictionary<int, ConsoleColor>();

        public static string ReadLine()
        {
            string line = Console.ReadLine();
            _lines.AppendLine(line);

            return line;
        }

        public static ConsoleKeyInfo ReadKey()
        {
            return ReadKey(false);
        }

        public static ConsoleKeyInfo ReadKey(bool intercept)
        {
            ConsoleKeyInfo info = Console.ReadKey(intercept);
            if (!intercept)
            {
                _lines.Append(info.KeyChar);
            }

            return info;
        }

        private static void ReinitializeDictionaries()
        {
            _foregroundChanges.Clear();
            _backgroundChanges.Clear();
            _foregroundChanges[0] = ConsoleColor.Gray;
            _backgroundChanges[0] = ConsoleColor.Black;
        }

        static MyConsole()
        {
            ReinitializeDictionaries();
        }

        private static StringBuilder _lines = new StringBuilder();

        public static ConsoleColor ForegroundColor
        {
            get
            {
                return Console.ForegroundColor;
            }

            set
            {
                Console.ForegroundColor = value;
                _foregroundChanges[_lines.Length] = value;
            }
        }

        public static ConsoleColor BackgroundColor
        {
            get
            {
                return Console.BackgroundColor;
            }

            set
            {
                Console.BackgroundColor = value;
                _backgroundChanges[_lines.Length] = value;
            }
        }

        /// <summary>
        /// Clear the console display WITHOUT CLEARING STREAM HISTORY.
        /// Using overridden console stream writing methods, such as those implemented by this class, while the display is cleared will produce UNDEFINED RESULTS.
        /// </summary>
        public static void ClearDisplay()
        {
            Console.Clear();
        }

        /// <summary>
        /// Restore the console display from recorded history.
        /// </summary>
        public static void RestoreDisplay()
        {
            ConsoleColor mainFg = ForegroundColor;
            ConsoleColor mainBg = BackgroundColor;

            Console.Clear();
            for (int i = 0; i < _lines.Length; i++)
            {
                ConsoleColor foreground;
                ConsoleColor background;

                if (_foregroundChanges.TryGetValue(i, out foreground))
                {
                    Console.ForegroundColor = foreground;
                }

                if (_backgroundChanges.TryGetValue(i, out background))
                {
                    Console.BackgroundColor = background;
                }

                Console.Write(_lines[i]);
            }

            Console.ForegroundColor = mainFg;
            Console.BackgroundColor = mainBg;
        }

        public static void WriteLine(string line)
        {
            Console.WriteLine(line);
            _lines.AppendLine(line);
        }

        public static void Write(string line)
        {
            Console.Write(line);
            _lines.Append(line);
        }

        public static void Clear()
        {
            Console.Clear();
            _lines.Clear();
            ReinitializeDictionaries();
        }

        public static void WriteLine(string line, params object[] format)
        {
            Console.WriteLine(line, format);
            _lines.AppendFormat(line + Console.Out.NewLine, format);
        }

        public static void Write(string line, params object[] format)
        {
            Console.Write(line);
            _lines.AppendFormat(line, format);
        }
    }
}
