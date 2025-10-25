using MemNet.Models;

namespace MemNet.Abstractions;

/// <summary>
///     知识图谱存储接口（复刻 Mem0 graph_store/base.py）
/// </summary>
public interface IGraphStore
{
    /// <summary>
    ///     添加实体关系
    /// </summary>
    Task AddRelationsAsync(List<EntityRelation> relations, CancellationToken ct = default);

    /// <summary>
    ///     搜索相关实体
    /// </summary>
    Task<List<Entity>> SearchEntitiesAsync(string entityName, CancellationToken ct = default);

    /// <summary>
    ///     获取实体的所有关系
    /// </summary>
    Task<List<EntityRelation>> GetRelationsAsync(string entityName, CancellationToken ct = default);

    /// <summary>
    ///     删除实体及其关系
    /// </summary>
    Task DeleteEntityAsync(string entityName, CancellationToken ct = default);
}