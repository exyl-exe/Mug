using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Mug.Tracks
{
    public class MugTrack
    {
        public const int MAX_INPUTS = 100000;//Jus de branle

        public string Name { get; set; }
        public int RefreshRate { get; set; }
        public int Offset { get; set; }
        public List<MugInput> Inputs { get; set; }

        public MugTrack()
        {
            Name = "Unnamed track";
            RefreshRate = 60;
            Offset = 0;
            Inputs = new List<MugInput>();
        }

        public MugTrack(int refreshRate)
        {
            RefreshRate = refreshRate;
            Inputs = new List<MugInput>();
        }

        public MugTrack GetPlayableTrack(int targetRefreshRate)
        {
            var fixedTrack = new MugTrack(targetRefreshRate);
            foreach (var input in this.Inputs)
            {
                var fixedInput = new MugInput();
                fixedInput.key = input.key;
                fixedInput.type = input.type;
                fixedInput.frame = (int)Math.Round((float)fixedTrack.RefreshRate * (float)(input.frame+Offset) / (float)this.RefreshRate);//TODO bad offset add
                fixedInput.position = input.position;
                fixedTrack.Inputs.Add(fixedInput);
            }
            fixedTrack.Inputs.Sort(new Comparison<MugInput>((MugInput i1, MugInput i2) => (int)i1.position-(int)i2.position));
            return fixedTrack;
        }

        public bool Save(string name)
        {
            Name = name;
            return TrackFilesManager.Save(this);
        }
    }
}
