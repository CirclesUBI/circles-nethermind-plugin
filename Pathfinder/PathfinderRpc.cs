using System.Diagnostics;
using System.Text;

namespace Circles.Index.Pathfinder;

public class RpcEndpoint
{
    private readonly string _rpcUrl;
    public RpcEndpoint(string rpcUrl)
    {
        _rpcUrl = rpcUrl;
    }

    public async Task<(string resultBody, Stopwatch spentTime)> Call(string requestJsonBody)
    {
        Stopwatch requestStopWatch = new();
        requestStopWatch.Start();

        StringContent content = new(requestJsonBody, Encoding.UTF8, "application/json");

        using HttpClient client = new();
        using HttpResponseMessage rpcResult = await client.PostAsync(_rpcUrl, content);
        await using Stream responseStream = await rpcResult.Content.ReadAsStreamAsync();
        using StreamReader streamReader = new(responseStream);
        string responseBody = await streamReader.ReadToEndAsync();

        requestStopWatch.Stop();

        return (responseBody, requestStopWatch);
    }
}
