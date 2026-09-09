using Xunit;

// The sky has two pieces of ambient state -- the observer's longitude and the world's axial tilt --
// and both are ambient for the same reason: their readers run from a render pass to an instrument in
// a player's hand, and threading them through every signature would put the world into the shape of
// every caller. A test that sets one of them is therefore setting it for the whole process, so the
// suite runs one class at a time rather than leaving that to chance. It costs nothing measurable:
// this is a few hundred milliseconds of arithmetic with no I/O in it.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
