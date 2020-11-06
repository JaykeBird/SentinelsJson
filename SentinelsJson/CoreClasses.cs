using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SentinelsJson
{
    public class UserData
    {
        public string Provider { get; set; } = "Local/Unknown";
        public string? Id { get; set; }
        public string? DisplayName { get; set; }
        //public string Gender { get; set; }
        public List<Email> Emails { get; set; }
        //public Name UserName { get; set; }
        public List<Photo> Photos { get; set; }
        [JsonProperty("profileUrl")]
        public string? ProfileUrl { get; set; }

        //public class Name { public string FamilyName { get; set; } public string GivenName { get; set; } }
        public class Email
        {
            public string Value { get; set; } = "";
            [JsonProperty("type")]
            public string? Type { get; set; }
        }

        public class Photo { public string? Value { get; set; } }

        public UserData()
        {
            Emails = new List<Email>();
            Photos = new List<Photo>();
        }

        public UserData(bool preset)
        {
            if (preset)
            {
                Provider = "local";
                Id = "null";
                DisplayName = "";
                ProfileUrl = "";
            }

            Emails = new List<Email>();
            Photos = new List<Photo>();
        }
    }

    public class Speed
    {
        public string? Base { get; set; }
        public string? WithArmor { get; set; }
        public string? Fly { get; set; }
        public string? Swim { get; set; }
        public string? Climb { get; set; }
        public string? Burrow { get; set; }
        [JsonProperty("tempModifiers")]
        public string? TempModifier { get; set; }
    }

    public class Equipment
    {
        public string? Name { get; set; }
        public string? Location { get; set; }
        public string? Type { get; set; }
        public string? Quantity { get; set; }
        public string? Weight { get; set; }
        public string? Notes { get; set; }
    }

    public class Feat
    {
        public string Name { get; set; } = "";
        public string? Type { get; set; }
        public string? Notes { get; set; }
        public string? School { get; set; }
        public string? Subschool { get; set; }
    }
}