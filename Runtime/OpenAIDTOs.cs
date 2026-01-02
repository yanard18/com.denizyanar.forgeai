using System;
using System.Collections.Generic;

namespace DenizYanar.AITool
{
    // WRAPPER FOR THE REQUEST
    [Serializable]
    public class ChatRequest
    {
        public string model = "gpt-4o"; // Or "gpt-4-turbo"
        public List<Message> messages = new List<Message>();
        public float temperature = 0.7f;
    }

    [Serializable]
    public class Message
    {
        public string role;
        public string content;

        public Message(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    // WRAPPER FOR THE RESPONSE
    [Serializable]
    public class ChatResponse
    {
        public List<Choice> choices;
    }

    [Serializable]
    public class Choice
    {
        public Message message;
    }
}