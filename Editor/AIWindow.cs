using DenizYanar.ForgeAI;
using DenizYanar.ForgeAI.Editor;
using UnityEditor;
using UnityEngine;

public class AIWindow : EditorWindow
{
    private string userPrompt = "";
    private string aiResponse = "";
    private string apiKey = "YOUR_API_KEY_HERE"; // Ideally store this in EditorPrefs
    private bool isProcessing = false;

    [MenuItem("Tools/Unity 6 AI Agent")]
    public static void ShowWindow()
    {
        GetWindow<AIWindow>("AI Agent");
    }

    private void OnGUI()
    {
        GUILayout.Label("AI Assistant (Unity 6 Core)", EditorStyles.boldLabel);

        // API Key Field (Password mask)
        apiKey = EditorGUILayout.PasswordField("API Key", apiKey);

        GUILayout.Space(10);
        GUILayout.Label("Your Prompt:");
        userPrompt = EditorGUILayout.TextArea(userPrompt, GUILayout.Height(100));

        GUILayout.Space(10);

        if (isProcessing)
        {
            GUILayout.Label("Thinking...", EditorStyles.helpBox);
        }
        else
        {
            if (GUILayout.Button("Ask AI"))
            {
                // Fire and forget the async void method
                ProcessRequest(); 
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("Response:");
        EditorGUILayout.TextArea(aiResponse, GUILayout.Height(200));
    }

    // Helper to bridge UI and Async logic
    private async void ProcessRequest()
    {
        // 1. Retrieve key from the new Preferences script
        var storedKey = AIProjectPreferences.APIKey;

        // 2. Validate
        if (string.IsNullOrEmpty(storedKey)) 
        {
            // Bonus: Allow user to click the error to open Preferences directly
            if (EditorUtility.DisplayDialog("Missing API Key", 
                    "You need to set your OpenAI API Key in Preferences before using this tool.", 
                    "Open Preferences", "Cancel"))
            {
                SettingsService.OpenUserPreferences("Preferences/AI Assistant");
            }
            return;
        }

        isProcessing = true;
        
        // 3. Send Request using the stored key
        var result = await AIClient.SendRequestAsync(userPrompt, storedKey);
        
        aiResponse = result ?? "Failed to get response.";
        isProcessing = false;
        Repaint();
    }
}