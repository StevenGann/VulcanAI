using System.Net.Http;
using System.Threading.Tasks;

namespace VulcanAI.Connectors;

public interface IHttpClient
{
    Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content);
} 