using Mug.MemoryAccessing;
using Mug.Tracks;
using System;
using System.Threading;

namespace Mug.Playing
{
    class MugPlayer
    {
        private int refreshRate;
        const int DELAY = 1000;

        public MugPlayer(int refreshRate)
        {
            this.refreshRate = refreshRate;
        }

        public void Play(MugTrack track)
        {
            var fixedTrack = track.GetPlayableTrack(refreshRate);
            PlayOnce(fixedTrack);
        }

        private void PlayOnce(MugTrack track)
        {
            MugAPI.LoadTrackInMemory(track);
            MugAPI.PlayCurrentTrack();
            while (MugAPI.IsPlaying())
            {
                Thread.Sleep(DELAY);
            }
        }
    }
}
