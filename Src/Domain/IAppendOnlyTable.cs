namespace Domain;

/// <summary>
/// Marker interface for entities that are append-only (immutable after creation).
/// These entities cannot be updated or deleted.
/// </summary>
public interface IAppendOnlyTable
{
}
