using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mug.UI
{
    static class MugConsole
    {
        private static void ClearBuffer()
        {
            while (Console.KeyAvailable)
            {
                Console.ReadKey(false);
            }
        }

        public static void WriteLine(string s)
        {
            Console.WriteLine(s);
        }

        public static void Write(string s)
        {
            Console.Write(s);
        }

        public static string AskString()
        {
            ClearBuffer();
            var txt = Console.ReadLine();
            return txt;
        }

        public static bool AskInt(out int result)
        {
            ClearBuffer();
            var txt = Console.ReadLine();
            return int.TryParse(txt, out result);
        }

        public static void WaitForNewline()
        {
            ClearBuffer();
            Console.ReadLine();
        }

        public static void WaitForKeypress()
        {
            ClearBuffer();
            Console.ReadKey();
        }
    }
}
