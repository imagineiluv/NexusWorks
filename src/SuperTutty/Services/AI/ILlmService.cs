using System.Collections.Generic;
using System.Threading.Tasks;

namespace SuperTutty.Services.AI
{
    public interface ILlmService
    {
        Task<string> AnalyzeLogTemplatesAsync(IEnumerable<string> templates);
    }
}
