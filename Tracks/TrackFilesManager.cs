using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Mug.Tracks
{
    static class TrackFilesManager
    {
        public const string TRACK_DIRECTORY = "./Tracks/";
        public const string EXTENSION = ".mug";
        public static bool Save(MugTrack track)
        {
            bool success = true;
            try
            {
                StreamWriter writer = File.CreateText(TRACK_DIRECTORY+track.Name+EXTENSION);
                var serializer = new XmlSerializer(typeof(MugTrack));
                serializer.Serialize(writer, track);
                writer.Close();
            }
            catch (Exception)
            {
                success = false;
            }
            return success;
        }

        public static MugTrack Load(string name)
        {
            MugTrack track = null;
            try
            {
                var reader = File.OpenRead(name);
                var serializer = new XmlSerializer(typeof(MugTrack));
                var loadedTrack = (MugTrack)serializer.Deserialize(reader);
                track = loadedTrack;
                reader.Close();
            }
            catch (Exception)
            {
                track = null;
            }
            return track;
        }

        public static List<MugTrack> GetTracks()
        {
            var list = new List<MugTrack>();
            foreach(var fileName in Directory.GetFiles(TRACK_DIRECTORY))//TODO potential error
            {
                var potentialTrack = Load(fileName);
                if(potentialTrack != null)
                {
                    list.Add(potentialTrack);
                }
            }
            return list;
        }

        public static void Initialize()
        {
            if (!Directory.Exists(TRACK_DIRECTORY))
            {
                Directory.CreateDirectory(TRACK_DIRECTORY);
            }
        }
    }
}
