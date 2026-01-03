using System.Collections.Generic;
using System.Threading.Tasks;

namespace ForgeAI
{
    public class LLMRequest
    {
        public string ApiKey;
        public string Model;
        public List<ChatMessage> History;
        public float Temperature = 0.7f;
        public int MaxTokens = 2000;
    }

    public interface ILLMProvider
    {
        Task<string> SendRequestAsync(LLMRequest request);
    }
}