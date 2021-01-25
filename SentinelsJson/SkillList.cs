using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SentinelsJson
{
    public class SkillList
    {

        public static SkillList LoadFile(string filename)
        {
            try
            {
                using StreamReader file = File.OpenText(filename);
                JsonSerializer serializer = new JsonSerializer();

                SkillList? ss = serializer.Deserialize<SkillList>(new JsonTextReader(file));
                if (ss == null) ss = new SkillList();
                return ss;
            }
            catch (JsonReaderException)
            {
                //MessageBox.Show("The SkillList file for SentinelsJson was corrupted. SentinelsJson will continue with default SkillList.",
                //    "SkillList Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                SkillList sn = new SkillList();
                sn.Save(filename); // exception handling not needed for these as calling function handles exceptions
                return sn;
            }
            catch (FileNotFoundException)
            {
                SkillList sn = new SkillList();
                sn.Save(filename); // exception handling not needed for these as calling function handles exceptions
                return sn;
            }
        }

        public void Save(string filename)
        {
            DirectoryInfo di = Directory.GetParent(filename);

            if (!di.Exists)
            {
                di.Create();
            }

            using StreamWriter file = new StreamWriter(filename, false, new UTF8Encoding(false));
            JsonSerializer serializer = new JsonSerializer();
            serializer.Serialize(file, this);
        }

        public string Name { get; set; } = "Unnamed";

        [JsonProperty("skills")]
        public List<SkillListEntry>? SkillEntries { get; private set; }

    }

    public class SkillListEntry
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public string? Name { get; set; };

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string? DisplayName { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public string Modifier { get; set; } = "PER";

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string? InfoUrl { get; set; }
    }
}
