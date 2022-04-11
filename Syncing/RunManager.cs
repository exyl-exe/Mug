using Mug.Configuration;
using Mug.MemoryAccessing;
using Mug.Record;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mug.Syncing
{
    class RunManager
    {
        public int CurrentAttempt { get => GDAPI.GetCurrentAttempt(); }//TODO is currently playing a level check
        public int CurrentFrame { get => MugAPI.GetFrameCount(); }
        public int CurrentSubcycle { get => MugAPI.GetCurrentSubcycleCount(); }

        public bool IsPlayerDead { get => GDAPI.IsPlayerDead(); }

        public bool HasPlayerWon { get => GDAPI.HasPlayerWon(); }

        public bool IsInLevel { get => GDAPI.IsInLevel(); }//TODO, might be useless property

        public bool IsAttemptOnGoing(int attempt)
        {
            return !IsPlayerDead && ! HasPlayerWon && CurrentAttempt == attempt;
        }

        public void WaitForNextAttempt() 
        {
            var currentAttempt = CurrentAttempt;
            //Waiting for the next attempt to start
            while (currentAttempt == CurrentAttempt) ;
        }

        public void WaitForNextFrame()
        //Actually doesn't wait for next frame but wait for currentFrame to end
        //Meaning every action done after this function is called will be done on the next frame
        {
            var currentFrame = CurrentFrame;
            //TODO optimise performances by sleeping ?
            while (CurrentFrame == currentFrame) ;
            while (CurrentSubcycle < GDAPI.SUBCYCLE_PER_FRAME) ;
        }
    }
}
