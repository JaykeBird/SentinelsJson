using System;
using System.Collections.Generic;
using System.Text;

namespace SentinelsJson.Ild
{
    public class IldPropertyInfo
    {
        public IldPropertyInfo(string name, IldType type, string? displayName = null)
        {
            Name = name;
            IldType = type;
            if (displayName != null) DisplayName = displayName; else DisplayName = name;
        }

        public string Name { get; set; }

        public string DisplayName { get; set; }

        public IldType IldType { get; set; }

        public int? MinValue { get; set; }

        public int? MaxValue { get; set; }

    }
}
