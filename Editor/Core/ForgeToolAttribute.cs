using System;

namespace ForgeAI
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ForgeToolAttribute : Attribute
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public bool RequiresApproval { get; private set; }

        public ForgeToolAttribute(string name, string description, bool requiresApproval = true)
        {
            Name = name;
            Description = description;
            RequiresApproval = requiresApproval;
        }
    }
}
