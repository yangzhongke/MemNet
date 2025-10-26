using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MemNet.Internals;

public static class HttpResponseExtensions
{
    public static async Task EnsureSuccessWithContentAsync(this HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string content = await response.Content.ReadAsStringAsync();
        string message = $"Error: {(int)response.StatusCode} {response.ReasonPhrase}\nResponse Body: {content}";
        throw new HttpRequestException(message);
    }
}