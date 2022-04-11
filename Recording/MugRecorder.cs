using Mug.Configuration;
using Mug.MemoryAccessing;
using Mug.Playing;
using Mug.Syncing;
using Mug.Tracks;
using Mug.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mug.Record
{
    class MugRecording{
        public const int METADATA_SIZE_BYTES = sizeof(int) + sizeof(int) + sizeof(int) + sizeof(float);
        //Number of inputs, attempt, frames, end percent
        public const int ATTEMPT_OFFSET = sizeof(int);
        public const int FRAME_OFFSET = ATTEMPT_OFFSET+sizeof(int);
        public const int END_PERCENT_OFFSET = FRAME_OFFSET + sizeof(int);

        public MugTrack Track { get; set; }
        public int Attempt { get; set; }
        public int Frames { get; set; }
        public int EndPercent { get; set; }

        public int LoadMetadataFromBytes(byte[] metadata)
        {
            Attempt = BitConverter.ToInt32(metadata, ATTEMPT_OFFSET);
            Frames = BitConverter.ToInt32(metadata, FRAME_OFFSET);
            var value = BitConverter.ToSingle(metadata, END_PERCENT_OFFSET);
            value = (value >= 1) ? 100 : value * 100;
            EndPercent = (int)value;
            return BitConverter.ToInt32(metadata, 0);
        }
    }

    class MugRecorder
    {
        public const int MAX_BUFFERED_INPUTS = 100000;
        public const int MAX_BUFFERED_TRACKS = 3;
        const int REFRESH_DELAY = 1000;//milliseconds

        public bool HasToRecord { get; set; }

        public MugRecorder()
        {
            
        }

        public List<MugRecording> Record()
        {
            HasToRecord = true;
            var records = new List<MugRecording>();
            MugAPI.StartRecording();
            do
            {
                Thread.Sleep(REFRESH_DELAY);
                var newRecords = MugAPI.FetchCurrentRecords();
                records.AddRange(newRecords);
            } while (MugAPI.IsRecording() && GDAPI.IsInLevel());

            if (!GDAPI.IsInLevel())
            {
                MugAPI.StopRecording();
                while (HasToRecord)
                {
                    Thread.Sleep(REFRESH_DELAY);
                } 
            }
            return records;
        }

        public void StopRecordingAndSave()
        {
            HasToRecord = false;
            if (MugAPI.IsRecording())
            {
                MugAPI.StopRecordingAndSave();
            }   
        }
    }
}
