using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mug.UI
{
    abstract class GenericMenu {

        public List<MenuEntry> menuActions = new List<MenuEntry>();

        public void AddAction(string desc, Action action, bool isTerminating = false)
        {
            var newAction = new MenuEntry(desc, action, isTerminating);
            menuActions.Add(newAction);
        }

        public void Run()
        {
            bool finished = false;
            string choiceString;
            int choice;
            while (!finished)
            {
                PrintMenu();
                choiceString = Console.ReadLine();
                if (int.TryParse(choiceString, out choice) && choice >= 0 && choice < menuActions.Count)
                {
                    finished = menuActions[choice].Run();
                }
                else
                {
                    Console.WriteLine(GetInvalidActionText());
                }
            }
        }

        public void PrintMenu()
        {
            Console.WriteLine(GetPreChoiceText());
            for (int i = 0; i < menuActions.Count; i++)
            {
                Console.WriteLine(i + ") " + menuActions[i].GetDescription());
            }
        }

        protected abstract string GetPreChoiceText();
        protected abstract string GetInvalidActionText();
    }

    class MenuEntry
    {
        private string description;
        private Action entryAction;
        private bool isTerminating;

        public MenuEntry(string desc, Action action, bool isTerminating = false)
        {
            this.description = desc;
            this.entryAction = action;
            this.isTerminating = isTerminating;
        }

        public bool Run()
        {
            entryAction();
            return isTerminating;
        }

        public string GetDescription()
        {
            return description;
        }
    }
}
