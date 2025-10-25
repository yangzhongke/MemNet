namespace MemNet.Models;

/// <summary>
///     实体信息
/// </summary>
public class Entity
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
///     实体关系
/// </summary>
public class EntityRelation
{
    public Entity Source { get; set; } = new();
    public Entity Target { get; set; } = new();
    public string RelationType { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
}