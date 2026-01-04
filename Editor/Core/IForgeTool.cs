using System;

namespace ForgeAI
{
    /// <summary>
    /// Base interface for all ForgeAI tools. 
    /// Each tool is an object that handles its own parameter parsing and execution.
    /// </summary>
    public interface IForgeTool
    {
        /// <summary>
        /// Returns the full prompt definition for this tool, including Name, Description, and Usage.
        /// </summary>
        string GetPromptDefinition();

        /// <summary>
        /// Executes the tool with the provided raw text arguments.
        /// </summary>
        string Execute(string rawInput);
    }
}
