using System;

namespace ForgeAI
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class ForgeToolAttribute : Attribute
    {
        public string Description { get; }
        public string ParameterHints { get; }
        public bool RequiresApproval { get; }
        public bool SupportsUndo { get; }

        public ForgeToolAttribute(string description, string parameterHints = "", bool requiresApproval = true, bool supportsUndo = false)
        {
            Description = description;
            ParameterHints = parameterHints;
            RequiresApproval = requiresApproval;
            SupportsUndo = supportsUndo;
        }
    }
}
