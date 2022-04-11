using Mug.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mug.Configuration
{
    static class GDConfig
    {
        public static int RefreshRate { get; set; } = 60;

        public static void SelectRefreshRate()
        {
            MugConsole.WriteLine("Enter new refresh rate");
            var success = MugConsole.AskInt(out var newRefreshRate);
            if (success && newRefreshRate > 0)
            {
                RefreshRate = newRefreshRate;
                MugConsole.WriteLine("The refresh rate has been modified.");
            }
            else
            {
                MugConsole.WriteLine("This is not a valid refresh rate.");
            }
        }
    }
}
