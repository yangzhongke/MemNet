using MemNet.Abstractions;
using MemNet.Models;

namespace MemNet.GraphStores;

/// <summary>
///     内存图存储实现（用于开发和测试）
/// </summary>
public class InMemoryGraphStore : IGraphStore
{
    private readonly Dictionary<string, Entity> _entities = new();
    private readonly object _lock = new();
    private readonly List<EntityRelation> _relations = new();

    public Task AddRelationsAsync(List<EntityRelation> relations, CancellationToken ct = default)
    {
        lock (_lock)
        {
            foreach (var relation in relations)
            {
                // 添加实体
                _entities[relation.Source.Name] = relation.Source;
                _entities[relation.Target.Name] = relation.Target;

                // 添加关系
                _relations.Add(relation);
            }
        }

        return Task.CompletedTask;
    }

    public Task<List<Entity>> SearchEntitiesAsync(string entityName, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var results = _entities.Values
                .Where(e => e.Name.Contains(entityName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Task.FromResult(results);
        }
    }

    public Task<List<EntityRelation>> GetRelationsAsync(string entityName, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var results = _relations
                .Where(r => r.Source.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase) ||
                            r.Target.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Task.FromResult(results);
        }
    }

    public Task DeleteEntityAsync(string entityName, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _entities.Remove(entityName);
            _relations.RemoveAll(r =>
                r.Source.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase) ||
                r.Target.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));
        }

        return Task.CompletedTask;
    }
}