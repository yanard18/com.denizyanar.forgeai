# ForgeAI Design & Development Journal

This document is a journal for tracking the design decisions, challenges, architectural ideas, and development journey of the ForgeAI project. It's a space for reflection and a historical record of the "why" behind the "what."

---

## Guiding Principles

*   **Simplicity & Clarity**: The architecture and code should be as straightforward as possible.
*   **User in Control**: The AI is an assistant, not an autonomous agent. The user should always have the final say on destructive actions.
*   **Extensibility**: It should be easy to add new tools and capabilities without rewriting the core engine.

---

## Entry Template (Copy for new entries)

```
### DATE: YYYY-MM-DD - TITLE

**Context:**
(What was the situation or problem I was trying to solve?)

**Decision / Action:**
(What change did I make or what approach did I decide on?)

**Reasoning:**
(Why did I make this choice? What alternatives did I consider and why were they discarded?)

**Challenges Faced:**
(What difficulties did I encounter during this process?)

**Outcome / Reflection:**
(What was the result? What did I learn? What are the next steps or future implications?)
```

---

## Journal Entries

### 2024-07-31 - Initial Architecture: ReAct Pattern

**Context:**
The initial goal was to create an AI agent capable of performing multi-step tasks within Unity. A simple prompt-response model would not be sufficient, as the agent needs to interact with the environment (the Unity project) and see the results of its actions.

**Decision / Action:**
I chose to implement the **ReAct (Reason + Act)** pattern. The flow is:
1.  User gives a prompt.
2.  The LLM **reasons** about the prompt and generates a "Thought".
3.  The LLM decides on an **action** (a tool to use).
4.  The system parses the action, executes the tool, and captures the result (the "Observation").
5.  The Observation is fed back into the LLM's context for the next reasoning cycle.

**Reasoning:**
The ReAct pattern is a well-established and effective way to build basic agents. It's relatively simple to implement compared to more complex patterns like Reframe or "Chain of Thought with State Passing," making it ideal for a version 0.1. It provides a solid foundation for more advanced capabilities later. I considered a more complex "Reflective Agent" model but decided it was too ambitious for the initial prototype. The `ForgeAI-Presentation.md` contains more thoughts on this.

**Challenges Faced:**
-   **Prompt Engineering**: Getting the LLM to reliably output "Thought:" and "Action:" blocks in a parsable format was the main challenge. It required careful "role prompting" and providing clear examples in the system prompt.
-   **Tool Definition**: Deciding on the right level of abstraction for tools. Should a tool be "RenameFile" or a more general "MoveAsset"? I opted for the latter, as renaming is just moving within the same directory.

**Outcome / Reflection:**
The ReAct pattern works well for the initial set of file-based tools. It proves the core concept is viable. However, I can already see its limitations. For very complex tasks, the context window might fill up quickly with observations, and the agent can get stuck in loops if it makes a mistake. Future work should focus on making the agent more robust, perhaps by introducing a planning or self-correction step.

---
