namespace Ladder.Wave
{
    /// <summary>
    /// DB-1's change class for one changed object — the property D23 routes on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The classes are Siemens' own RUN-download table for the S7-1200 V4 column [R], as DB-1
    /// transcribes it:
    /// </para>
    /// <code>
    ///   FB/FC code change ............................ RUN
    ///   New / deleted FB, FC, DB, UDT ................ RUN
    ///   New / deleted OB, or OB property change ...... STOP
    ///   DB interface change (no memory reserve) ...... RUN (Init)  - resets, retentives included
    ///   Modified UDT ................................. RUN (Init)  on EVERY DB built on it
    ///   Modified retentivity settings ................ STOP
    ///   New / revised text lists ..................... STOP
    ///   Hardware configuration ....................... STOP
    ///   Comments, new PLC tags ....................... RUN
    /// </code>
    /// <para>
    /// THE ZERO VALUE IS <see cref="Unknown"/>, for the same reason
    /// <see cref="ConfigurationClass.Unknown"/> is: a dropped field, a zeroed struct or a caller who
    /// never set it must land on the branch that refuses, never on the branch that flows through a
    /// wave boundary. <see cref="ChangeRouter"/> REFUSES an unknown class rather than picking a queue
    /// for it — the same argument D32 Class C makes, that guessing what a thing entails is exactly
    /// what the policy exists to forbid.
    /// </para>
    /// <para>
    /// THIS TYPE DOES NOT COMPUTE A CLASS. DB-1 does, from the reference graph and its baseline, and
    /// DB-1 is not built. What is built here is the ROUTER that consumes the answer, and the refusals
    /// that keep an unclassified or self-contradictory change out of a wave.
    /// </para>
    /// </remarks>
    public enum ChangeClass
    {
        /// <summary>
        /// Nobody classified this change. NOT a synonym for "harmless": it is refused at admission,
        /// because a change whose class is unknown is a change whose CPU-stop consequence is unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// RUN — loaded with nothing disturbed. FB/FC code changes, new or deleted FB/FC/DB/UDT,
        /// comments, new PLC tags. Flows through a wave boundary (D23).
        /// </summary>
        Run = 1,

        /// <summary>
        /// RUN (Init) — loaded in RUN, but data is RESET, retentives included. A DB interface change
        /// without memory reserve, and every DB built on a modified UDT. Flows through a wave boundary
        /// like <see cref="Run"/> — the difference is that results predating it are invalid (DB-2's
        /// validity stamp), not that the queue changes.
        /// </summary>
        RunInit = 2,

        /// <summary>
        /// STOP — cannot be loaded without stopping the CPU. OB add/delete/property change (R1),
        /// modified retentivity settings, new or revised text lists, hardware configuration.
        /// ACCUMULATES IN THE DEFERRED QUEUE (D23) and lands only at a drain boundary (D24).
        /// </summary>
        Stop = 3,
    }

    /// <summary>
    /// What kind of thing changed. Used for two purposes and no others: the <see cref="ChangeRouter"/>
    /// cross-check against the kinds whose class the spec FIXES, and the object count that DB-4's
    /// twenty-object limit is expressed in.
    /// </summary>
    /// <remarks>
    /// <see cref="Unknown"/> is zero and is REFUSED by the router. That is not pedantry: the router's
    /// only defence against a mis-declared class is the fixed-kind table, and a change that will not
    /// say what kind of object it is cannot be held to it. An OB declared <see cref="ChangeClass.Run"/>
    /// is caught; an OB declared as kind <see cref="Unknown"/> and class <see cref="ChangeClass.Run"/>
    /// would not be, so the kind is required.
    /// </remarks>
    public enum ObjectKind
    {
        /// <summary>Not stated. Refused by <see cref="ChangeRouter"/>.</summary>
        Unknown = 0,

        /// <summary>An organisation block. Its class is FIXED to <see cref="ChangeClass.Stop"/> by R1.</summary>
        OrganizationBlock = 1,

        /// <summary>A function block.</summary>
        FunctionBlock = 2,

        /// <summary>A function.</summary>
        Function = 3,

        /// <summary>A global data block.</summary>
        GlobalDataBlock = 4,

        /// <summary>An instance data block. Depends on the FB it instantiates — see DB-4's example.</summary>
        InstanceDataBlock = 5,

        /// <summary>A PLC data type (UDT). Its blast radius is every DB built on it (DB-1).</summary>
        DataType = 6,

        /// <summary>A PLC tag table.</summary>
        TagTable = 7,

        /// <summary>A text list. Its class is FIXED to <see cref="ChangeClass.Stop"/> by DB-1.</summary>
        TextList = 8,

        /// <summary>Hardware configuration. Its class is FIXED to <see cref="ChangeClass.Stop"/> by DB-1.</summary>
        HardwareConfiguration = 9,

        /// <summary>A retentivity setting. Its class is FIXED to <see cref="ChangeClass.Stop"/> by DB-1.</summary>
        RetentivitySetting = 10,
    }

    /// <summary>
    /// D23's two queues. There are exactly two, and there is no third for "we are not sure" — that is
    /// a REFUSAL, not a queue.
    /// </summary>
    public enum DownloadQueue
    {
        /// <summary>
        /// Not routed. The zero value, so an unset field never reads as "may flow through a wave
        /// boundary".
        /// </summary>
        Unassigned = 0,

        /// <summary>
        /// RUN-class. Flows through normal wave boundaries — cheap, non-disruptive (§1.4).
        /// </summary>
        RunQueue = 1,

        /// <summary>
        /// STOP-class. Does NOT flow through a wave boundary; accumulates until the queue is drained
        /// because no test can make progress (D24), and the drain is the only place R8's disruptive
        /// mode is permitted.
        /// </summary>
        DeferredQueue = 2,
    }

    /// <summary>Why <see cref="ChangeRouter"/> refused to route a change into either queue.</summary>
    public enum RoutingRefusal
    {
        /// <summary>Not refused.</summary>
        None = 0,

        /// <summary>The object carried no usable name, so nothing downstream could refer to it.</summary>
        UnnamedObject = 1,

        /// <summary>
        /// The object did not say what KIND it is, so the fixed-kind cross-check below cannot be
        /// applied and a mis-declared class would pass unchallenged.
        /// </summary>
        UnknownObjectKind = 2,

        /// <summary>
        /// Nobody classified the change. Routing it anyway would be guessing whether it stops the CPU.
        /// Refused at admission, which is the cheap place to refuse (X-L).
        /// </summary>
        UnknownChangeClass = 3,

        /// <summary>
        /// The declared class contradicts the class the spec FIXES for that kind of object — an
        /// organisation block declared RUN, a text list declared RUN, a hardware-configuration or
        /// retentivity change declared RUN. Refused rather than silently corrected: a declaration this
        /// wrong means the classifier that produced it is wrong, and correcting the symptom hides that.
        /// </summary>
        ContradictsTheFixedClassForThatKind = 4,
    }
}
