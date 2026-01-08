# ForgeAI: A Unity Editor AI Assistant



ForgeAI is an experimental project aiming to develop an intelligent AI agent within the Unity Editor. Its primary goal is to empower Unity developers by autonomously performing actions to solve complex problems and automate repetitive tasks, such as creating Editor tools, manipulating scenes, or managing project assets.

Unlike existing solutions like Unity Muse, ForgeAI is designed to deeply understand the project's context, including reading operations, allowing it to take more informed and relevant actions. The initial motivation for this tool emerged from the challenges faced by a game development team struggling with complex Git operations, fixing wrongly named assets, and organizing project files.

This project also serves as the final project for a "Prompt Engineering" lecture, exploring advanced techniques for AI agent design and interaction within a practical application. The core architectural pattern for the AI's implementation is currently under experimentation.

## Core Concepts

-   **ReAct Engine**: At its heart, ForgeAI uses a ReAct loop. It **reasons** about your request, forms a plan, and then **acts** by executing one or more of its available tools. It observes the outcome and continues this loop until the task is complete.
-   **Conversational UI**: Interact with the agent through a simple chat-based window right inside the Unity Editor.
-   **Extensible Tooling**: The agent's capabilities are defined by a set of tools. Adding new tools is as simple as creating a C# class, making the agent easy to extend.
-   **Safety First**: By default, any action that modifies your project (like writing or deleting files) requires your explicit approval, giving you full control.

## Getting Started

### Prerequisites

-   Unity 2022.3 or newer (Project uses version `6000.3` which is a placeholder for newer versions)

### Installation

1.  Open the Unity Editor.
2.  Navigate to **Window > Package Manager**.
3.  Click the **`+`** icon in the top-left corner and select **"Add package from git URL..."**.
4.  Enter the repository's HTTPS or SSH Git URL and click **"Add"**.

### Configuration

1.  Before you can use the agent, you must provide an API key.
2.  Navigate to **Edit > Preferences > ForgeAI**.
3.  Select your desired AI Provider (e.g., OpenAI, Gemini).
4.  Enter your secret API key into the "API Key" field. Your key is stored securely in your local Editor Preferences and is never shared.
5.  (Optional) Specify the exact model name you wish to use (e.g., `gpt-4o`, `gemini-1.5-pro-latest`).

## Example Problems ForgeAI Intends to Solve

<!-- Add your notes on example problems here. For example:
- Automating the creation of script templates.
- Organizing assets based on naming conventions.
- Performing complex Git operations with natural language commands.
-->

## Branch Structure

<!-- Describe your branch structure and old approaches here. For example:
- **main**: The most stable version.
- **feat/new-tool-system**: Experimentation with a new tooling architecture.
- **poc/react-implementation**: A proof-of-concept branch for the ReAct pattern.
-->

## Further Documentation

-   **[Changelog](./CHANGELOG.md)**: A log of all major updates, features, and bug fixes.
-   **[Design Journal](./doc/DESIGN_JOURNAL.md)**: Detailed notes on design decisions, challenges faced, and architectural evolution.
