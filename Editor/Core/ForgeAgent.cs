using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace ForgeAI
{
    public class ForgeAgent
    {
        private List<ForgeInteraction> interactions = new List<ForgeInteraction>();
        private const int MAX_STEPS = 5;

        public IReadOnlyList<ForgeInteraction> Interactions => interactions;
        public ForgeInteraction CurrentInteraction => interactions.LastOrDefault();
        
        public event Action OnHistoryChanged; 
        public event Action<bool> OnProcessingStateChanged;
        public event Action<string> OnError;
        public event Action OnActionProposed;

        public ForgeAgent() { }

        public void ClearHistory()
        {
            interactions.Clear();
            OnHistoryChanged?.Invoke();
        }

        public async Task ChatAsync(string userPrompt)
        {
            if (string.IsNullOrWhiteSpace(userPrompt)) return; 
            
            // Start new log session if this is the beginning of a conversation
            if (interactions.Count == 0)
            {
                ForgeLogger.StartNewSession();
            }

            var interaction = new ForgeInteraction(userPrompt);
            interactions.Add(interaction);
            OnHistoryChanged?.Invoke();

            await RunReActLoop(interaction, 0);
        }

        public async Task ApproveActionAsync()
        {
            var interaction = CurrentInteraction;
            if (interaction == null || interaction.ProposedActions.Count == 0) return;

            NotifyProcessing(true);
            try
            {
                interaction.ActionResults.Clear();
                foreach (var action in interaction.ProposedActions)
                {
                    var observation = ReActEngine.ExecuteTool(action);
                    interaction.ActionResults.Add(observation);
                }

                interaction.Status = "Action Executed";
                interaction.ProposedActions.Clear(); // Clear pending

                await RunReActLoop(interaction, 0); 
            }
            catch (Exception e)
            {
                HandleException(e);
            }
        }

        public async Task RejectActionAsync()
        {
            var interaction = CurrentInteraction;
            if (interaction == null || interaction.ProposedActions.Count == 0) return;

            NotifyProcessing(true);
            try
            {
                string msg = "User rejected the plan.";
                interaction.ActionResults.Add(msg);
                interaction.Status = "Action Rejected";
                interaction.ProposedActions.Clear();

                await RunReActLoop(interaction, 0);
            }
            catch (Exception e)
            {
                HandleException(e);
            }
        }

        private async Task RunReActLoop(ForgeInteraction current, int startStep)
        {
            NotifyProcessing(true);
            int step = startStep;

            try
            {
                while (step < MAX_STEPS)
                {
                    current.ProposedActions.Clear();

                    var context = BuildContext(current);
                    string response = await LLMClient.SendRequest(context);

                    if (response.StartsWith("Error:"))
                    {
                        current.Status = "Error";
                        current.ErrorMessage = response;
                        OnError?.Invoke(response);
                        break;
                    }

                    current.AIResponse = response; 
                    OnHistoryChanged?.Invoke();

                    var actions = ReActEngine.ExtractAllActions(response);
                    
                    if (actions.Count > 0)
                    {
                        current.ProposedActions = actions;

                        bool needsApproval = false;
                        foreach (var act in actions)
                        {
                            if (ReActEngine.RequiresApproval(act.tool))
                            {
                                needsApproval = true;
                                break;
                            }
                        }
                        
                        if (needsApproval)
                        {
                            current.Status = "Waiting for Approval";
                            NotifyProcessing(false);
                            OnActionProposed?.Invoke();
                            return; 
                        }
                        else
                        {
                            current.ActionResults.Clear();
                            foreach (var act in actions)
                            {
                                var obs = ReActEngine.ExecuteTool(act);
                                current.ActionResults.Add(obs);
                            }
                        }
                    }
                    else
                    {
                        current.Status = "Completed";
                        break;
                    }

                    step++;
                    await Task.Yield();
                }
            }
            catch (Exception e)
            {
                HandleException(e);
            }
            finally
            {
                if (current.Status != "Waiting for Approval")
                    NotifyProcessing(false);
            }
        }

        private List<ChatMessage> BuildContext(ForgeInteraction current)
        {
            var msgs = new List<ChatMessage>();
            msgs.Add(new ChatMessage { role = "system", content = ReActEngine.GetSystemPrompt() });

            // Sliding Window: Keep last 10 interactions
            int startIndex = Math.Max(0, interactions.Count - 11);
            
            for (int i = startIndex; i < interactions.Count; i++)
            {
                var interaction = interactions[i];
                if (interaction == current) continue;

                msgs.Add(new ChatMessage { role = "user", content = interaction.UserPrompt });
                if (!string.IsNullOrEmpty(interaction.AIResponse))
                {
                    msgs.Add(new ChatMessage { role = "assistant", content = interaction.AIResponse });
                }
                
                if (interaction.ActionResults.Count > 0)
                {
                    // STRICT Digest and Discard: Always hide past observations.
                    // The AI knows it must have summarized this in its previous 'Thought'.
                    msgs.Add(new ChatMessage { role = "user", content = "Observation: [Hidden. Processed in previous turn.]" });
                }
            }

            // Current Interaction (Active Context) - Always show full observations
            msgs.Add(new ChatMessage { role = "user", content = current.UserPrompt });
            
            if (current.ActionResults.Count > 0)
            {
                 if (!string.IsNullOrEmpty(current.AIResponse))
                    msgs.Add(new ChatMessage { role = "assistant", content = current.AIResponse });
                 
                 string combinedObs = string.Join("\n", current.ActionResults);
                 msgs.Add(new ChatMessage { role = "user", content = "Observations:\n" + combinedObs });
            }

            return msgs;
        }

        private void HandleException(Exception e)
        {
            if (CurrentInteraction != null)
            {
                CurrentInteraction.Status = "Error";
                CurrentInteraction.ErrorMessage = e.Message;
            }
            NotifyProcessing(false);
            OnError?.Invoke(e.Message);
            Debug.LogException(e);
        }

        private void NotifyProcessing(bool isProcessing)
        {
            OnProcessingStateChanged?.Invoke(isProcessing);
        }
    }
}