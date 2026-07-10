namespace OpennessCli.Model;

public enum BlockType
{
    OB,
    FB,
    FC,
    DB,
}

public sealed record BlockInfo(
    string Name,
    BlockType Type,
    int Number,
    string Language,
    bool IsSafety,
    string Path,
    bool IsConsistent);
