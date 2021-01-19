using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SentinelsJson
{
    public class SentinelsSheet
    {
        public static SentinelsSheet LoadJsonFile(string filename)
        {
            using (StreamReader file = File.OpenText(filename))
            {
                try
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.DefaultValueHandling = DefaultValueHandling.Ignore;
                    serializer.NullValueHandling = NullValueHandling.Ignore;
                    serializer.ContractResolver = new CamelCasePropertyNamesContractResolver();
                    serializer.Error += (object? sender, Newtonsoft.Json.Serialization.ErrorEventArgs e) => ErrorHandler(sender, e, filename);
                    SentinelsSheet ss = (SentinelsSheet)serializer.Deserialize(file, typeof(SentinelsSheet))!;
                    ss.SetupSheet();
                    return ss;
                }
                catch (JsonReaderException e)
                {
                    throw new InvalidDataException("This file does not match the format for JSON. Check if it isn't corrupted by opening it in Notepad or another text editor.", e);
                }
            }
        }

        private static void ErrorHandler(object? _, Newtonsoft.Json.Serialization.ErrorEventArgs e, string filename)
        {
            throw new InvalidDataException("This file \"" + filename + "\" does not match the format for JSON. Check if it isn't corrupted by opening it in Notepad or another text editor.", e.ErrorContext.Error);
        }

        public static SentinelsSheet LoadJsonText(string text)
        {
            SentinelsSheet ss = JsonConvert.DeserializeObject<SentinelsSheet>(text);
            ss.SetupSheet();
            return ss;
        }

        public static SentinelsSheet CreateNewSheet(string name, int level, int pointsPerLevel = 10, int level0Points = 0, UserData? userdata = null)
        {
            string newjson = "{\"_id\":\"-1\"," +
                "\"user\":{\"provider\":\"local\",\"id\":\"null\",\"displayName\":\"-\"," +
                "\"username\":\"\",\"profileUrl\":\"\",\"emails\":[]}," +
                "\"name\":\"" + name + "\",\"modified\":\"" + string.Concat(DateTime.UtcNow.ToString("s"), ".000Z") +
                "\",\"charLevel\":" + level + ",\"ecl\":" + level + ",\"pointsPerLevel\":" + level + ",\"pointsLevel0\":" + level + "," +
                "\"defenseActions\":2 }";

            SentinelsSheet ps = LoadJsonText(newjson);
            if (userdata != null) ps.Player = userdata;

            return ps;
        }

        public string SaveJsonText(bool indented = false, string file = "StoredText", bool updateModified = false)
        {
            if (updateModified) Modified = string.Concat(DateTime.UtcNow.ToString("s"), ".000Z");

            JsonSerializerSettings jss = new JsonSerializerSettings();
            jss.DefaultValueHandling = DefaultValueHandling.Ignore;
            jss.NullValueHandling = NullValueHandling.Ignore;
            jss.ContractResolver = new CamelCasePropertyNamesContractResolver();
            jss.Error += (object? sender, Newtonsoft.Json.Serialization.ErrorEventArgs e) => ErrorHandler(sender, e, file);
            return JsonConvert.SerializeObject(this, indented ? Formatting.Indented : Formatting.None, jss);
        }

        public void SaveJsonFile(string file, bool indented = false)
        {
            File.WriteAllText(file, SaveJsonText(indented, file, true));
        }

        // Meta properties

        [JsonProperty("id", Order = -50)]
        public string Id { get; set; } = "-1";

        [JsonProperty(Order = -2, DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        public string? Modified { get; set; } // ISO 8601 datetime (with UTC mark)

        [JsonProperty("user", Order = -5)]
        public UserData? Player { get; set; }

        [JsonProperty(Order = 50, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Notes { get; set; } = "";

        // Roleplaying characteristics

        [JsonProperty(Order = -3)]
        public string Name { get; set; } = "";
        public string? Alignment { get; set; }
        public string? Homeland { get; set; }
        public string? Deity { get; set; }
        public string? Languages { get; set; }

        // physical characteristics
        public string? Gender { get; set; }
        public string? Age { get; set; }
        public string? Height { get; set; }
        public string? Weight { get; set; }
        public string? Race { get; set; }
        public string? Size { get; set; }
        public string? Hair { get; set; }
        public string? Eyes { get; set; }

        public Speed? Speed { get; set; }

        // Abilities / Potential

        [JsonIgnore]
        public int Strength { get; set; }
        [JsonIgnore]
        public int Perception { get; set; }
        [JsonIgnore]
        public int Endurance { get; set; }
        [JsonIgnore]
        public int Charisma { get; set; }
        [JsonIgnore]
        public int Intellect { get; set; }
        [JsonIgnore]
        public int Agility { get; set; }
        [JsonIgnore]
        public int Luck { get; set; }

        [JsonIgnore]
        public int PotStrength { get; set; }
        [JsonIgnore]
        public int PotPerception { get; set; }
        [JsonIgnore]
        public int PotEndurance { get; set; }
        [JsonIgnore]
        public int PotCharisma { get; set; }
        [JsonIgnore]
        public int PotIntellect { get; set; }
        [JsonIgnore]
        public int PotAgility { get; set; }
        [JsonIgnore]
        public int PotLuck { get; set; }

        /// <summary>Used to determine if the abilities structure in JSON was present or not</summary>
        [JsonIgnore]
        public bool AbilitiesPresent { get; set; }
        /// <summary>Used to determine if the potential structure in JSON was present or not</summary>
        [JsonIgnore]
        public bool PotentialPresent { get; set; }

        [JsonProperty("abilities")]
        public Dictionary<string, string> RawAbilities { get; set; } = new Dictionary<string, string>();
        [JsonProperty("potential")]
        public Dictionary<string, string> RawPotential { get; set; } = new Dictionary<string, string>();

        // sheet settings

        [JsonProperty("sheetSettings", DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string?>? SheetSettings { get; set; }

        // Sentinels specific general values

        [JsonProperty("charLevel")]
        public int BaseLevel { get; set; } = 0;

        [JsonProperty("ecl")]
        public int ECL { get; set; } = 0;


        public int UsedBulsh { get; set; } = 0;

        [JsonProperty("powerStat")]
        public string PowerStatName { get; set; } = "PER";

        // Sentinels attributes / CP

        [JsonProperty("pointsPerLevel")]
        public int PointsPerLevel { get; set; } = 10;

        [JsonProperty("pointsLevel0")]
        public int Level0Points { get; set; } = 10;

        // Sentinels combat
        // Unlike PathfinderJson, Sentinels's combat values are mostly calculated by existing values without much modification or variability
        // thus, the big main values (i.e. "CMB", "Melee attack bonus", etc.) won't be stored in the JSON, as they're calcuated based upon the ones below
        // (if desired in the future, I can have the program write in the final calculated values as well)

        [JsonProperty("trainedPr")]
        public int TrainedProwess { get; set; } = 0;

        [JsonProperty("brawlAgi")]
        public bool BrawlUseAgi { get; set; } = false;

        [JsonProperty("meleeAgi")]
        public bool MeleeUseAgi { get; set; } = false;

        public int DefenseActions { get; set; }

        public int CmbMisc { get; set; } = 0;

        public int CmdMisc { get; set; } = 0;

        public int MmbMisc { get; set; } = 0;

        public int MmdMisc { get; set; } = 0;

        public Armor Armor { get; set; } = new Armor();

        // Other general stats

        public Dictionary<string, Save> Saves { get; set; } = new Dictionary<string, Save>();

        // Feats

        public List<Feat> Feats { get; set; } = new List<Feat>();

        private void SetupSheet()
        {
            if (RawAbilities != null)
            {
                if (RawAbilities.Count == 0)
                {
                    DefaultLoadAbilities();
                }
                else
                {
                    foreach (KeyValuePair<string, string> item in RawAbilities)
                    {
                        switch (item.Key)
                        {
                            case "luk":
                                try { Luck = int.Parse(item.Value); } catch (FormatException) { Luck = 0; }
                                break;
                            case "int":
                                try { Intellect = int.Parse(item.Value); } catch (FormatException) { Intellect = 0; }
                                break;
                            case "cha":
                                try { Charisma = int.Parse(item.Value); } catch (FormatException) { Charisma = 0; }
                                break;
                            case "str":
                                try { Strength = int.Parse(item.Value); } catch (FormatException) { Strength = 0; }
                                break;
                            case "agi":
                                try { Agility = int.Parse(item.Value); } catch (FormatException) { Agility = 0; }
                                break;
                            case "end":
                                try { Endurance = int.Parse(item.Value); } catch (FormatException) { Endurance = 0; }
                                break;
                            case "per":
                                try { Perception = int.Parse(item.Value); } catch (FormatException) { Perception = 0; }
                                break;
                            default:
                                break;
                        }
                    }
                    AbilitiesPresent = true;
                }
            }
            else
            {
                DefaultLoadAbilities();
            }

            if (RawPotential != null)
            {
                if (RawPotential.Count == 0)
                {
                    DefaultLoadPotential();
                }
                else
                {
                    foreach (KeyValuePair<string, string> item in RawPotential)
                    {
                        switch (item.Key)
                        {
                            case "luk":
                                try { PotLuck = int.Parse(item.Value); } catch (FormatException) { PotLuck = 20; }
                                break;
                            case "int":
                                try { PotIntellect = int.Parse(item.Value); } catch (FormatException) { PotIntellect = 20; }
                                break;
                            case "cha":
                                try { PotCharisma = int.Parse(item.Value); } catch (FormatException) { PotCharisma = 20; }
                                break;
                            case "str":
                                try { PotStrength = int.Parse(item.Value); } catch (FormatException) { PotStrength = 20; }
                                break;
                            case "agi":
                                try { PotAgility = int.Parse(item.Value); } catch (FormatException) { PotAgility = 20; }
                                break;
                            case "end":
                                try { PotEndurance = int.Parse(item.Value); } catch (FormatException) { PotEndurance = 20; }
                                break;
                            case "per":
                                try { PotPerception = int.Parse(item.Value); } catch (FormatException) { PotPerception = 20; }
                                break;
                            default:
                                break;
                        }
                    }
                    PotentialPresent = true;
                }
            }
            else
            {
                DefaultLoadPotential();
            }

            // also, let's check the saving throws
            if (Saves.Count == 0)
            {
                Saves["fort"] = new Save();
                Saves["reflex"] = new Save();
                Saves["will"] = new Save();
            }
            else
            {
                if (!Saves.ContainsKey("fort")) Saves["fort"] = new Save();
                if (!Saves.ContainsKey("reflex")) Saves["reflex"] = new Save();
                if (!Saves.ContainsKey("will")) Saves["will"] = new Save();
            }

            void DefaultLoadAbilities()
            {
                // the user didn't fill in the base ability scores for the character at all
                // so just set everything to 0
                Luck = 0;
                Intellect = 0;
                Charisma = 0;
                Strength = 0;
                Agility = 0;
                Endurance = 0;
                Perception = 0;
                AbilitiesPresent = false;
            }

            void DefaultLoadPotential()
            {
                // the user didn't fill in the potential scores for the character at all
                // so just set everything to 20 (default value)
                PotLuck = 20;
                PotIntellect = 20;
                PotCharisma = 20;
                PotStrength = 20;
                PotAgility = 20;
                PotEndurance = 20;
                PotPerception = 20;
                PotentialPresent = false;
            }
        }
    }

    public class Save
    {
        public Save()
        {

        }

        public Save(int ranks, int temp)
        {
            Ranks = ranks;
            Misc = temp;
        }

        public int Ranks { get; set; } = 0;

        [JsonProperty("misc")]
        public int Misc { get; set; } = 0;
    }

    public class Armor
    {
        public int Equipment { get; set; } = 0;
        public int Natural { get; set; } = 0;
        public int Attributes { get; set; } = 0;
        public int Shield { get; set; } = 0;
        public int Misc { get; set; } = 0;
    }
}