using System;

namespace ForgeAI
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class ForgeToolAttribute : Attribute
    {
        public string Description { get; }
        public string ParameterHints { get; }

        public ForgeToolAttribute(string description, string parameterHints = "")
        {
            Description = description;
            ParameterHints = parameterHints;
        }
    }
}
