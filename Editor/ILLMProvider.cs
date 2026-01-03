using System.Collections.Generic;
using System.Threading.Tasks;

namespace ForgeAI
{
    public interface ILLMProvider
    {
        Task<string> SendRequest(string apiKey, string model, List<ChatMessage> history);
    }
}
