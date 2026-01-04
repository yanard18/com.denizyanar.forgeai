
# ForgeAI: An AI-Powered Unity Editor Assistant

## 1. Introduction to ForgeAI

ForgeAI is an AI-powered agent integrated directly into the Unity Editor. It is designed to assist with a variety of development tasks, from simple file operations to more complex code manipulation. The goal of ForgeAI is to streamline the development workflow by allowing developers to use natural language to perform tasks that would normally require manual intervention or scripting. This project is a practical application of prompt engineering techniques and AI agent design patterns, specifically the ReAct (Reason + Act) pattern.

## 2. Core Concepts & Techniques

### 2.1. Prompt Engineering

The effectiveness of ForgeAI is heavily reliant on carefully crafted prompts. The following prompt engineering techniques are used:

*   **Role Prompting**: The AI is given a clear role: "You are an intelligent Unity Editor Assistant (ForgeAI)". This sets the context and encourages the AI to behave in a way that is consistent with the desired persona.
*   **Context Injection**: The system prompt dynamically includes a list of available tools, along with their descriptions and usage instructions. This provides the AI with the necessary information to perform its tasks.
*   **Output Formatting**: The prompt explicitly defines the expected output format, which is a crucial part of the ReAct pattern. The AI is instructed to use "Thought:" to explain its reasoning and "Action:" to specify the tool it wants to use. This structured output is essential for programmatically extracting and executing tool calls.

### 2.2. The ReAct Pattern

ForgeAI is built around the ReAct (Reason + Act) pattern. This pattern enables the AI to reason about a problem, decide on a course of action, and then execute that action using a set of predefined tools. Here's how it's implemented in ForgeAI:

1.  **Reasoning**: The user provides a prompt. The `ForgeAgent` sends this prompt, along with the conversation history and system prompt, to the LLM. The LLM then generates a "Thought" process, which is a textual explanation of its reasoning.
2.  **Action**: Based on its reasoning, the LLM decides which tool to use and generates an "Action" block in its response (e.g., `Action: ListFiles`).
3.  **Execution**: The `ReActEngine` parses the LLM's response, extracts the action (tool name) and its input, and then executes the corresponding tool.
4.  **Observation**: The result of the tool execution is captured as an "Observation" and is fed back into the conversation history. This allows the AI to see the outcome of its action and decide on the next step.

This loop of "Reason -> Act -> Observe" continues until the task is complete. This is a powerful paradigm that allows the AI to tackle complex, multi-step problems.

### 2.3. Dynamic Tooling System

ForgeAI features a flexible and extensible tooling system. Tools are defined as C# classes that implement the `IForgeTool` interface. The `[ForgeTool]` attribute is used to provide metadata about the tool, such as its name, description, and whether it requires user approval before execution.

The `ReActEngine` uses reflection to automatically discover all classes with the `[ForgeTool]` attribute at runtime. This means that adding new tools is as simple as creating a new class and adding the attribute, without needing to modify the core engine.

## 3. Alternative Approaches: Reflective Agent

While the current ReAct implementation is powerful, a more advanced approach would be to evolve it into a "Reflective Agent" (also known as "Chain of Thought with State Passing"). This would involve:

*   **State Management**: The agent would maintain a more explicit state, representing its current understanding of the problem and the steps it has taken.
*   **Self-Correction**: After each action, the agent would not only observe the result but also "reflect" on whether the action brought it closer to its goal. If not, it could adjust its plan and try a different approach. This would make the agent more robust and less likely to get stuck in loops.
*   **Dynamic Planning**: Instead of a simple "Reason -> Act" loop, the agent would generate a multi-step plan, execute it, and then revise the plan based on the outcomes.

This would be a significant enhancement to ForgeAI, enabling it to handle more complex and open-ended tasks.

## 4. The Power of ForgeAI in Unity

The integration of an AI agent directly into the Unity Editor has immense potential:

*   **Accelerated Prototyping**: Quickly create placeholder assets, generate scripts, and set up scenes using natural language.
*   **Automated Refactoring**: Perform project-wide code changes, such as renaming variables or restructuring classes, with a single command.
*   **On-the-fly Debugging**: Ask the agent to read log files, inspect game objects, or even suggest fixes for common errors.
*   **Enhanced Accessibility**: Lower the barrier to entry for new Unity developers by providing a more intuitive, conversational interface.

## 5. ForgeAI vs. gemini-cli

While a general-purpose tool like `gemini-cli` is powerful for a wide range of tasks, a specialized agent like ForgeAI offers several key advantages within the Unity ecosystem:

*   **Context-Awareness**: ForgeAI has direct access to the Unity project's assets, settings, and the `AssetDatabase`. This allows it to perform operations that are specific to Unity's workflow.
*   **Domain-Specific Tools**: ForgeAI's tools are designed specifically for Unity development (e.g., `MoveAsset`, `DeleteAsset`), making them more efficient and reliable than generic file system commands.
*   **Integrated UI**: ForgeAI is a visual tool within the Unity Editor, providing a more user-friendly experience than a command-line interface.
*   **Safety**: By using Unity's own APIs (like `AssetDatabase.MoveAsset`), ForgeAI ensures that operations are performed safely and correctly, preserving metadata and avoiding project corruption.

## 6. Call to Action: How Can I Improve ForgeAI?

This project is a starting point. Here are some questions for discussion and feedback:

*   **Robustness**: How can the ReAct loop be made more robust? What are the best strategies for error handling and self-correction?
*   **Tooling**: What other tools would be most beneficial for a Unity developer? How can the tool discovery and execution process be improved?
*   **User Experience**: What is the most intuitive way for a developer to interact with an AI agent in the editor? How can the feedback from the agent be made clearer and more useful?
*   **Advanced Agents**: What are the practical steps to evolve ForgeAI from a ReAct agent to a more advanced "Reflective Agent"?

Thank you for your time and feedback.
