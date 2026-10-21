using Microsoft.AspNetCore.Http;

namespace LoginDirectorTests.Shared
{
    /// <summary>
    /// In-memory ISession implementation for controller unit testing.
    /// </summary>
    public class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        public bool IsAvailable => true;
        public string Id => "TestSessionId";
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value);
    }
}