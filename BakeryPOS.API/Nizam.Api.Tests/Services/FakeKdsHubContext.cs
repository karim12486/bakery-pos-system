using Nizam.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Capturing fake <see cref="IHubContext{KdsHub}"/> for service tests — records the last
/// group + method broadcast so tests can assert SignalR targeting without a real hub.
/// </summary>
internal sealed class FakeKdsHubContext : IHubContext<KdsHub>
{
    private readonly CapturingClients _clients = new();
    public IHubClients Clients => _clients;
    public IGroupManager Groups { get; } = new NoOpGroupManager();
    public string? LastGroup => _clients.LastGroup;
    public string? LastMethod => _clients.LastProxy?.LastMethod;

    private sealed class CapturingClients : IHubClients
    {
        public string? LastGroup;
        public CapturingProxy? LastProxy;

        public IClientProxy Group(string groupName)
        {
            LastGroup = groupName;
            return LastProxy = new CapturingProxy();
        }

        public IClientProxy All => new CapturingProxy();
        public IClientProxy AllExcept(IReadOnlyList<string> e) => new CapturingProxy();
        public IClientProxy Client(string c) => new CapturingProxy();
        public IClientProxy Clients(IReadOnlyList<string> c) => new CapturingProxy();
        public IClientProxy GroupExcept(string g, IReadOnlyList<string> e) => new CapturingProxy();
        public IClientProxy Groups(IReadOnlyList<string> g) => new CapturingProxy();
        public IClientProxy User(string u) => new CapturingProxy();
        public IClientProxy Users(IReadOnlyList<string> u) => new CapturingProxy();
    }

    private sealed class CapturingProxy : IClientProxy
    {
        public string? LastMethod;
        public Task SendCoreAsync(string method, object?[] args, CancellationToken ct = default)
        {
            LastMethod = method;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string c, string g, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string c, string g, CancellationToken ct = default) => Task.CompletedTask;
    }
}
