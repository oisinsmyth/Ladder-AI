namespace OpennessCli.Model;

// A PLC tag table has no Number/Language/Safety concept — confirmed real, 2026-07-14
// (reflecting on the installed DLL for S1 item 26's own follow-on tag-table work): PlcTagTable
// exposes only Name and its own Tags/SystemConstants/UserConstants compositions, nothing else
// BlockInfo's own shape assumes. Kept as its own minimal record rather than retrofitting
// BlockInfo with fields that would be meaningless here, same reasoning as PlcType's own
// ImportTypes returning bare names instead of BlockInfo.
public sealed record TagTableInfo(string Name, string Path);
