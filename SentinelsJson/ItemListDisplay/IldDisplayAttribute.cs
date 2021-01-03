using System;
using System.Collections.Generic;
using System.Text;

namespace SentinelsJson.Ild
{
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
    public sealed class IldDisplayAttribute : Attribute
    {
        // See the attribute guidelines at 
        //  http://go.microsoft.com/fwlink/?LinkId=85236

        // This is a positional argument
        public IldDisplayAttribute() { }

        public string? Name { get; set; } = null;

        public bool Ignore { get; set; } = false;

        public int? MinValue { get; set; } = null;
        public int? MaxValue { get; set; } = null;
    }
}
