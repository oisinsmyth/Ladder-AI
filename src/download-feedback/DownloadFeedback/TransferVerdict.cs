namespace Ladder.Download
{
    /// <summary>
    /// Whether anything was transferred to the device. THREE values, and the third is not a
    /// courtesy.
    /// </summary>
    /// <remarks>
    /// <see cref="Undetermined"/> must never collapse into <see cref="NothingTransferred"/>
    /// (spec §9c). They differ in what the caller is entitled to do next: after
    /// <see cref="NothingTransferred"/> the device is known to still hold what it held, and after
    /// <see cref="Undetermined"/> nothing whatever is known and the device must be read before it is
    /// trusted. A two-valued verdict makes the second look like the first, which is the more
    /// dangerous direction.
    /// </remarks>
    public enum TransferVerdict
    {
        /// <summary>
        /// No conclusion is available. The download produced no result, or produced one that names
        /// nothing loaded and does not say it skipped the transfer either.
        /// </summary>
        Undetermined = 0,

        /// <summary>
        /// At least one item was reported loaded BY NAME. Positive evidence; see
        /// <see cref="DownloadFeedback.TransferredItemCount"/> for how much.
        /// </summary>
        Transferred,

        /// <summary>
        /// TIA stated positively that it did not transfer, because the target was already
        /// up-to-date. Success with nothing moved.
        /// </summary>
        NothingTransferred,
    }
}
