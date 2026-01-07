using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Ripple.NET;

internal class RippleService : IHostedService
{
    private readonly Dictionary<string, HashSet<APICall>> apiCalls = new();
    private readonly object apiLock = new object();

    public RippleService(IEnumerable<EndpointDataSource> endpointSources, IHostApplicationLifetime lifetime)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            lock (apiLock)
            {
                foreach (var source in endpointSources)
                {
                    foreach (var endpoint in source.Endpoints)
                    {
                        if (endpoint.DisplayName == null)
                            continue;
                        apiCalls[endpoint.DisplayName] = [];
                    }
                }
            }
        });
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

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
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

    private class TransWithParent
    {
        private Transaction tx;
        
        public TransWithParent(Transaction tx, APICall parent)
        {
            this.tx = tx;
            this.Parent = parent;
        }

        public APICall Parent { get; private set; }
        public IReadOnlyCollection<Transaction> Children => tx.Children;
        public bool ReadOnly => tx.ReadOnly;
        public IReadOnlyCollection<string> ReadTypes => tx.ReadTypes;
        public IReadOnlyCollection<string> WriteTypes => tx.WriteTypes;
    }

    private class Operations
    {
        public List<TransWithParent> Reads { get; private set;} = [];
        public List<TransWithParent> Writes { get; private set;} = [];
    }

    private class Edge
    {
        public TransWithParent From { get; private set; }
        public TransWithParent To { get; private set; }

        public Type Type { get; private set; }

        public bool IsWrite { get; private set; }

        public Edge(TransWithParent from, TransWithParent to, Type type, bool isWrite)
        {
            From = from;
            To = to;
            Type = type;
            IsWrite = isWrite;
        }
    }

    public void GetGraph()
    {
        List<APICall> writeCalls = new();
        lock (apiLock)
        {
            writeCalls = apiCalls.Values.SelectMany(x => x).Where(api => !api.ReadOnly).ToList();
        }

        var operations = CreateOperations(writeCalls);
    }

    private static Dictionary<string, Operations> CreateOperations(List<APICall> writeCalls)
    {
        Dictionary<string, Operations> operations = new();

        Queue<TransWithParent> queue = new Queue<TransWithParent>(writeCalls.SelectMany(api =>
                                                api.Transactions.Select(tx => new TransWithParent(tx, api))));

        while (queue.Count > 0)
        {
            TransWithParent current = queue.Dequeue();

            foreach (var readType in current.ReadTypes)
            {
                if (!operations.TryGetValue(readType, out var ops))
                {
                    ops = new Operations();
                    operations[readType] = ops;
                }
                ops.Reads.Add(current);
            }

            foreach (var writeType in current.WriteTypes)
            {
                if (!operations.TryGetValue(writeType, out var ops))
                {
                    ops = new Operations();
                    operations[writeType] = ops;
                }
                ops.Writes.Add(current);
            }

            foreach (var child in current.Children)
            {
                queue.Enqueue(new TransWithParent(child, current.Parent));
            }
        }
        return operations;
    }

    internal Interceptor Interceptor { get; private set; } = new Interceptor();

}
