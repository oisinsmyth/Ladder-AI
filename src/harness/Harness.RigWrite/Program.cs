using Harness.RigWrite;

// The composition root — and the whole of this build's device story, in one expression.
//
// RigWriteCli takes a client factory. This is the only one it is ever given, and it throws. There is
// no Sharp7Client here, no reference to Sharp7 from this project, and no configuration that swaps
// this for something that opens a socket: rig-write plans a governed write and stops.
return RigWriteCli.Run(
    args,
    Environment.GetEnvironmentVariable,
    () => throw Arming.Refuse());
