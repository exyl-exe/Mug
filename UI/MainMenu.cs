using Mug.MemoryAccessing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Xml.Serialization;
using Mug.Playing;
using Mug.Syncing;
using Mug.Record;
using System.IO;
using Mug.UI;
using Mug.Tracks;
using Mug.Configuration;

namespace Mug
{
    class MainMenu : GenericMenu
    {
        public MainMenu()
        {
            BuildMenu();
        }

        private void BuildMenu()
        {
            AddAction("Exit", Exit, true);
            AddAction("Record a track.", RecordAction);
            AddAction("Play a track.", PlayAction);
        }

        private void SpamTest()
        {
            var track = new MugTrack(60);
            for (int i = 1; i < 500; i++)
            {
                var input1 = new MugInput();
                input1.key = InputKey.Spacebar;
                input1.type = InputType.Press;
                input1.frame = (2 * i);

                var input2 = new MugInput();
                input2.key = InputKey.Spacebar;
                input2.type = InputType.Release;
                input2.frame = (2 * i + 1);

                track.Inputs.Add(input1);
                track.Inputs.Add(input2);
            }

            PlayTest(track);
        }

        private void PlayTest(MugTrack track)
        {
            var realTrack = track.GetPlayableTrack(60);
            MugAPI.LoadTrackInMemory(realTrack);
            MugAPI.PlayCurrentTrack();
        }

        private void RecordAction()
        {
            var recordingMenu = new RecordingMenu();
            recordingMenu.Run();
        }

        private void PlayAction()
        {
            var playingMenu = new PlayingMenu();
            playingMenu.Run();
        }

        private void Exit()
        {
            GDAPI.RevertAllGDAlterations();
        }

        protected override string GetInvalidActionText()
        {
            return "Invalid action";
        }

        protected override string GetPreChoiceText()
        {
            return "*************************\n"
                  +"What do you want to do ?";
        }
    }
}
