# ForgeAI: A Unity Editor AI Assistant

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

ForgeAI is an intelligent, conversational AI agent integrated directly into the Unity Editor. It leverages the power of Large Language Models (LLMs) to understand natural language commands, allowing you to perform complex editor tasks, automate workflows, and manage your project with simple instructions.

This project is built as a practical application of modern AI agent design, specifically using the **ReAct (Reason + Act)** pattern.

## Core Concepts

-   **ReAct Engine**: At its heart, ForgeAI uses a ReAct loop. It **reasons** about your request, forms a plan, and then **acts** by executing one or more of its available tools. It observes the outcome and continues this loop until the task is complete.
-   **Conversational UI**: Interact with the agent through a simple chat-based window right inside the Unity Editor.
-   **Extensible Tooling**: The agent's capabilities are defined by a set of tools. Adding new tools is as simple as creating a C# class, making the agent easy to extend.
-   **Safety First**: By default, any action that modifies your project (like writing or deleting files) requires your explicit approval, giving you full control.

## Features

ForgeAI comes with a set of built-in tools to streamline your workflow:

-   **File & Asset Management**:
    -   `ListFiles`: List files and directories.
    -   `MoveAsset`: Move or rename assets, correctly updating Unity's AssetDatabase.
    -   `DeleteAsset`: Safely delete assets from your project.
-   **Content & Code Editing**:
    -   `ReadFile`: Read the content of any text-based file.
    -   `WriteFile`: Create new files (scripts, shaders, text files) or overwrite existing ones.
    -   `ReplaceText`: Perform precise text replacement within files—ideal for refactoring.

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

## How to Use

1.  Open the ForgeAI window by navigating to **Window > ForgeAI Assistant** (Shortcut: `Ctrl+Alt+F` or `Cmd+Alt+F`).
2.  Type a command into the input box at the bottom and press Enter or click "Send".
3.  The agent will show its thought process and, if necessary, present a plan for your approval.
4.  Click **"Approve Plan"** to execute the proposed actions or **"Reject Plan"** to cancel.
5.  The agent will execute the tools and show you the results of each action.

**Example Prompt:**

> Create a new C# script at `Assets/Scripts/Player/PlayerMovement.cs` and make it log "Player moved" in the Update method.

## Further Documentation

-   **[Changelog](./CHANGELOG.md)**: A log of all major updates, features, and bug fixes.
-   **[Design Journal](./doc/DESIGN_JOURNAL.md)**: Detailed notes on design decisions, challenges faced, and architectural evolution.

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.
