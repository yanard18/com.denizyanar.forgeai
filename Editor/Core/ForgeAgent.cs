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
        
        // Events
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

            // Start new interaction
            var interaction = new ForgeInteraction(userPrompt);
            interactions.Add(interaction);
            OnHistoryChanged?.Invoke();

            await RunReActLoop(interaction, 0);
        }

        public async Task ApproveActionAsync()
        {
            var interaction = CurrentInteraction;
            if (interaction == null || interaction.ProposedAction == null) return;

            NotifyProcessing(true);
            try
            {
                // Execute Tool
                var observation = ReActEngine.ExecuteTool(interaction.ProposedAction);
                interaction.ActionResult = observation;
                interaction.Status = "Action Executed";
                interaction.ProposedAction = null; // Clear pending

                // Continue loop with observation
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
            if (interaction == null || interaction.ProposedAction == null) return;

            NotifyProcessing(true);
            try
            {
                string msg = "User rejected the action.";
                interaction.ActionResult = msg;
                interaction.Status = "Action Rejected";
                interaction.ProposedAction = null;

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
                    // Build Context from ALL interactions + current partial state
                    var context = BuildContext(current);

                    string response = await LLMClient.SendRequest(context);

                    if (response.StartsWith("Error:"))
                    {
                        current.Status = "Error";
                        current.ErrorMessage = response;
                        OnError?.Invoke(response);
                        break;
                    }

                    // Update current interaction with latest thought/response
                    current.AIResponse = response; 
                    OnHistoryChanged?.Invoke();

                    // Check for Tool Action
                    var jsonAction = ReActEngine.ExtractActionJson(response);
                    if (!string.IsNullOrEmpty(jsonAction))
                    {
                        var action = ReActEngine.ParseToolAction(jsonAction);
                        if (action != null && !string.IsNullOrEmpty(action.tool))
                        {
                            bool needsApproval = ReActEngine.RequiresApproval(action.tool);
                            
                            if (needsApproval)
                            {
                                current.ProposedAction = action;
                                current.Status = "Waiting for Approval";
                                NotifyProcessing(false);
                                OnActionProposed?.Invoke();
                                return; // PAUSE
                            }
                            else
                            {
                                // Auto-execute
                                var observation = ReActEngine.ExecuteTool(action);
                                current.ActionResult = observation; // Store result
                                
                                // In a multi-step loop, we append this to context. 
                                // Ideally, ForgeInteraction should support a LIST of steps.
                                // For simplicity/MVP: We overwrite ActionResult and continue.
                                // To fix context loss: We append the Observation to the context in the next loop.
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

            foreach (var i in interactions)
            {
                if (i == current) continue; // Handle current separately

                msgs.Add(new ChatMessage { role = "user", content = i.UserPrompt });
                if (!string.IsNullOrEmpty(i.AIResponse))
                {
                    msgs.Add(new ChatMessage { role = "assistant", content = i.AIResponse });
                }
                if (!string.IsNullOrEmpty(i.ActionResult))
                {
                    msgs.Add(new ChatMessage { role = "user", content = "Observation: " + i.ActionResult });
                }
            }

            // Add current
            msgs.Add(new ChatMessage { role = "user", content = current.UserPrompt });
            // If we are mid-loop (e.g. resuming after rejection), we need to handle that state.
            // Complex state reconstruction omitted for brevity, assuming linear flow for now.
            if (!string.IsNullOrEmpty(current.ActionResult))
            {
                 // If we have a result but are continuing, it means we did an action.
                 if (!string.IsNullOrEmpty(current.AIResponse))
                    msgs.Add(new ChatMessage { role = "assistant", content = current.AIResponse });
                 
                 msgs.Add(new ChatMessage { role = "user", content = "Observation: " + current.ActionResult });
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
