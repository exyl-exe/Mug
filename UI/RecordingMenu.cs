using Mug.Configuration;
using Mug.MemoryAccessing;
using Mug.Record;
using Mug.Tracks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mug.UI
{
    class RecordingMenu : GenericMenu
    {
        private MugRecorder recorder;

        public RecordingMenu()
        {
            AddAction("Return to main menu", Exit, true);
            AddAction("Start recording", Record);
            AddAction("Configure refresh rate tracks are recorded on", GDConfig.SelectRefreshRate);
        }

        private void Exit()
        {

        }

        private void Record()
        {
            if (!GDAPI.IsInLevel())
            {
                MugConsole.WriteLine("Launch a level before attempting to record");
                return;
            }

            var stopThread = new Thread(new ThreadStart(RecordStopThread));
            recorder = new MugRecorder();
            MugConsole.WriteLine("Recording started (will record once the next attempt starts)");
            MugConsole.WriteLine("Press enter to stop recording.");
            stopThread.Start();
            var records = recorder.Record();
            MugConsole.WriteLine("Recording stopped !");
            if (records.Count <= 0)
            {
                MugConsole.WriteLine("No attempts were recorded ...");
                return;
            }
            var recordToSave = ChooseRecord(records, out var cancel);
            if(cancel)
            {
                Console.WriteLine("Returning to menu ...");
                return;
            }
            SaveRecord(recordToSave);
        }

        private MugRecording ChooseRecord(List<MugRecording> records, out bool cancel)
        {
            cancel = false;
            MugConsole.WriteLine("Choose which record to save ! (enter -1 not to save any)");
            DisplayRecords(records);
            var success = MugConsole.AskInt(out var recordNumber);
            while (!success || recordNumber >= records.Count || recordNumber < -1)
            {
                MugConsole.WriteLine("Please enter one of the records number.");
                success = MugConsole.AskInt(out recordNumber);
            }
            if (recordNumber == -1)
            {
                cancel = true;
                return null;
            }
            return records[recordNumber];
        }

        private void SaveRecord(MugRecording record)
        {
            MugConsole.WriteLine("Give a name to this recording :");
            var name = MugConsole.AskString();
            record.Track.Name = name;
            var success = TrackFilesManager.Save(record.Track);

            if (success)
            {
                MugConsole.WriteLine("Track saved successfully");
            } else
            {
                MugConsole.WriteLine("Track couldn't be saved ...");
            }
        }

        private void DisplayRecords(List<MugRecording> records)
        {
            for (var i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                var recordString = String.Format(
                    "{0}) "
                    + "Attempt {1}\t"
                    + "{2:0.00}s\t"
                    + "{3}%",
                    i, rec.Attempt, (float)rec.Frames / (float)rec.Track.RefreshRate, rec.EndPercent);
                MugConsole.WriteLine(recordString);
            }
        }
        private void RecordStopThread()
        {
            MugConsole.WaitForNewline();
            recorder.StopRecordingAndSave();
        }

        protected override string GetInvalidActionText()
        {
            return "Invalid choice.";
        }

        protected override string GetPreChoiceText()
        {
            return  "---------------------\n"
                  + $"Tracks will currently be recorded as if GD runs at {GDConfig.RefreshRate}fps\n"
                  + "Choose what to do:";
        }
    }
}
