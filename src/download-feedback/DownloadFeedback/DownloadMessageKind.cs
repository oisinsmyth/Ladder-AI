namespace Ladder.Download
{
    /// <summary>
    /// What a single download message was recognised as.
    /// </summary>
    /// <remarks>
    /// Every message lands in exactly one of these, and <see cref="Unrecognised"/> is a first-class
    /// member of the set rather than a silent drop. Four distinct vocabularies appeared across the
    /// recorded runs and each new download option produced one nobody predicted; a classifier that
    /// keeps only what it knows goes blind exactly when something new happens.
    /// </remarks>
    public enum DownloadMessageKind
    {
        /// <summary>
        /// Not classified. Reported, counted and carried in full — never dropped.
        /// </summary>
        Unrecognised = 0,

        /// <summary>
        /// <c>'&lt;name&gt;' was loaded successfully.</c> — a named program object. The quotes are
        /// what distinguish this from <see cref="NonObjectLoad"/>, and they are TIA's, not ours.
        /// </summary>
        ObjectLoad,

        /// <summary>
        /// <c>Hardware configuration was loaded successfully.</c> /
        /// <c>Connection configuration was downloaded successfully.</c> /
        /// <c>Routing configuration was loaded successfully.</c> — an unquoted subject, and note
        /// that TIA uses two different verbs across those three. Positive transfer evidence just
        /// like <see cref="ObjectLoad"/>, counted separately because it is not a program object and
        /// so does not belong in the manifest that is cross-checked against the change tracker.
        /// </summary>
        NonObjectLoad,

        /// <summary><c>PLC_1 stopped.</c></summary>
        CpuStopped,

        /// <summary><c>PLC_1 started.</c></summary>
        CpuStarted,

        /// <summary>
        /// <c>The software has not been loaded, because it is up-to-date.</c> — success with
        /// nothing transferred.
        /// </summary>
        UpToDate,

        /// <summary>
        /// A group header: a node with children whose own text is a bare name rather than a
        /// sentence — the device root <c>PLC_1</c>, and the hardware download's
        /// <c>Hardware configuration</c> node that CONTAINS the loads rather than being one.
        /// Recognised structurally (children, and no terminating period), never by name.
        /// </summary>
        Container,
    }
}
