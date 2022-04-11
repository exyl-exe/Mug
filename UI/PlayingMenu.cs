using Mug.UI;
using Mug.Tracks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mug.MemoryAccessing;
using Mug.Configuration;
using Mug.Playing;

namespace Mug.UI
{
    class PlayingMenu : GenericMenu
    {   
        protected override string GetInvalidActionText()
        {
            return "Invalid choice.";
        }

        protected override string GetPreChoiceText()
        {
            return "--------------------------\n"
                  +$"Tracks will currently be played on {GDConfig.RefreshRate}fps\n"
                  +"Choose what to do :";
        }

        public PlayingMenu()
        {
            AddAction("Return to main menu", Exit, true);
            AddAction("Choose a track to play",Play);
            AddAction("Change the refresh rate tracks will be played on", GDConfig.SelectRefreshRate);
        }

        public void Exit()
        {
            
        }

        public void Play()
        {
            if (!GDAPI.IsInLevel())
            {
                MugConsole.WriteLine("Warning : currently not playing a level, enter a level before playing a track.");
            }

            var tracks = TrackFilesManager.GetTracks();

            if (tracks.Count == 0)
            {
                MugConsole.WriteLine($"There are no tracks in the track folder ({TrackFilesManager.TRACK_DIRECTORY})");
                return;
            }

            var cancel = false;
            var track = SelectTrackToPlay(tracks, ref cancel);

            if (cancel)
            {
                return;
            }
            else if(track == null)
            {
                MugConsole.WriteLine("The selected track couldn't be loaded..");
            } else
            {
                if (track.RefreshRate != GDConfig.RefreshRate)
                {
                    MugConsole.Write($"Warning : The selected track's refresh rate ({track.RefreshRate}fps) is different from the current configuration. ");
                    MugConsole.Write("The track might not work because of frame alignment and physics changes. Do you want to play it anyway ? [y/n]\n");
                    var choice = MugConsole.AskString();
                    if (!choice.ToLower().StartsWith("y"))
                    {
                        return;
                    }
                }
                PlayTrack(track);
            }
        }


        public MugTrack SelectTrackToPlay(List<MugTrack> tracks, ref bool cancel)
        {
            MugConsole.WriteLine($"Available tracks (enter -1 to cancel):");
            for(var i=0; i<tracks.Count; i++)
            {
                MugConsole.WriteLine($"{i}) {tracks[i].Name}");
            }
            var success = MugConsole.AskInt(out var trackNumber);
            while (!success && trackNumber > tracks.Count && trackNumber < -1)
            {
                MugConsole.WriteLine("Please enter the track's number displayed above.");
                success = MugConsole.AskInt(out trackNumber); ;
            }

            if(trackNumber == -1)
            {
                MugConsole.WriteLine("Returning to playing menu ..");
                cancel = true;
                return null;
            }

            return tracks[trackNumber];
        }

        public void PlayTrack(MugTrack t)
        {
            var player = new MugPlayer(GDConfig.RefreshRate);
            if (GDAPI.IsInLevel())
            {
                MugConsole.WriteLine("Track will play once the next attempt has started ...");
                player.Play(t);
                MugConsole.WriteLine("The track has been played.");
            } else
            {
                MugConsole.WriteLine("Please launch a level before playing a track.");
            }
            
        }
    }
}
