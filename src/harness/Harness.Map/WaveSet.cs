namespace Harness.Map;

/// <summary>
/// One slot's claim on the mirror: how many registers its vector occupies and how many its results do.
///
/// <para>A slot is per (agent, methodology) — D26b — and supplies a COLUMN of vectors that differ only
/// in VALUES. "Same methodology" means same register layout, which is exactly why these two numbers
/// are properties of the slot rather than of a vector: the layout is constant across every wave index
/// and the map is derived once per wave set.</para>
///
/// <para><see cref="ResultRegisters"/> must be at least 1. A slot that publishes nothing cannot be
/// judged, and a map that allocates it looks exactly like a map that allocated a real one — the
/// "empty is not clean" failure (FI-44) in map form.</para>
/// </summary>
public sealed record SlotRequest(string SlotId, int VectorRegisters, int ResultRegisters);

/// <summary>
/// The wave set being mapped: the slots that will share one download, in the order they are allocated.
///
/// <para><b>Order is part of the map.</b> Slot ordinals decide addresses, addresses feed the map hash,
/// and the client's bounds check is <c>base + index x slot_size</c>. Reordering the same slots produces
/// a different map, which is correct — it is a different allocation.</para>
/// </summary>
public sealed record WaveSetRequest(MirrorGeometry Geometry, IReadOnlyList<SlotRequest> Slots);
