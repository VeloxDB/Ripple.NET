using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Ripple.NET;

internal class RippleService : IHostedService
{
    private readonly Dictionary<string, HashSet<APICall>> apiCalls = new();
    private readonly object apiLock = new object();

    public RippleService(IEnumerable<EndpointDataSource> endpointSources, IHostApplicationLifetime lifetime)
    {
    }

    public void Clear()
    {
        lock (apiLock)
        {
            foreach (HashSet<APICall> calls in apiCalls.Values)
            {
                calls.Clear();
            }
        }
    }

    public void AddAPICall(APICall call)
    {
        lock (apiLock)
        {
            if (!apiCalls.ContainsKey(call.Name))
            {
                apiCalls[call.Name] = new HashSet<APICall>();
            }
            apiCalls[call.Name].Add(call);
        }
    }

    public Dictionary<string, List<APICall>> GetAPICalls()
    {
        lock (apiLock)
        {
            return apiCalls.ToDictionary(entry => entry.Key, entry => new List<APICall>(entry.Value));
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}

	internal Interceptor Interceptor { get; private set; } = new Interceptor();
}
