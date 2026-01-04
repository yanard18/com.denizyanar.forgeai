using System;

namespace ForgeAI
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public class ForgeToolAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }
        public bool RequiresApproval { get; }

        public ForgeToolAttribute(string name, string description, bool requiresApproval = true)
        {
            Name = name;
            Description = description;
            RequiresApproval = requiresApproval;
        }
    }
}
