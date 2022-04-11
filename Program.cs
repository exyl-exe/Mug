using Mug.MemoryAccessing;
using Mug.Tracks;
using Mug.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Mug
{
    class Program
    {
        static void Main(string[] args)
        {
            PrintIntro();
            var initialized = InitializeAPI();
            //TODO on exit
            TrackFilesManager.Initialize();
            if (initialized)
            {
                var mainMenu = new MainMenu();
                mainMenu.Run();
            }
        }

        static void OnExit(object sender, EventArgs e)
        {
            GDAPI.RevertAllGDAlterations();
        }

        static void PrintIntro()
        {
            MugConsole.WriteLine("Welcome to Mug");
        }

        static bool InitializeAPI()
        {
            var initialized = GDAPI.Initialize();
            bool giveup = false;
            while (!initialized && !giveup)
            {
                MugConsole.WriteLine("Couldn't find GD process. Retry ? [y/n]");
                var choice = MugConsole.AskString();
                if (choice.ToLower().StartsWith("y"))
                {
                    initialized = GDAPI.Initialize();
                }
                else
                {
                    giveup = true;
                }
            }
            return !giveup;
        }
    }
}
