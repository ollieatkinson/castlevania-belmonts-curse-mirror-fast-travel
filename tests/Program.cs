using MirrorFastTravel;

static void Check(bool expected, bool actual, string scenario)
{
    if (expected != actual) throw new Exception($"{scenario}: expected {expected}, got {actual}");
}

// A menu can report hidden during its closing frame. Neither that frame nor a
// burst of high-FPS frames should start loading before the settling interval.
var gate = new TravelStartGate();
Check(false, gate.CanStart(false, 10f, 100), "map still visible");
Check(false, gate.CanStart(true, 10f, 100), "first hidden frame");
Check(false, gate.CanStart(true, 10.1f, 120), "many frames but too little time");
Check(true, gate.CanStart(true, 10.25f, 130), "map remained closed");

// A long frame should not allow loading immediately on the next frame.
gate.Reset();
Check(false, gate.CanStart(true, 20f, 200), "new request");
Check(false, gate.CanStart(true, 21f, 201), "one long frame");
Check(true, gate.CanStart(true, 21.1f, 202), "two frames and enough real time");

// Reopening the menu, a popup, or losing gameplay readiness must restart the
// interval instead of inheriting readiness from before the interruption.
gate.Reset();
Check(false, gate.CanStart(true, 30f, 300), "initial readiness");
Check(false, gate.CanStart(false, 30.1f, 310), "readiness interrupted");
Check(false, gate.CanStart(true, 31f, 320), "ready again");
Check(false, gate.CanStart(true, 31.1f, 330), "new interval not complete");
Check(true, gate.CanStart(true, 31.25f, 340), "new interval complete");

// Cancelling and confirming another destination must not reuse the old timer.
gate.Reset();
Check(false, gate.CanStart(true, 40f, 400), "new destination starts fresh");
Check(true, gate.CanStart(true, 40.25f, 410), "new destination settles");
gate.Reset();
Check(false, gate.CanStart(true, 50f, 500), "next destination starts fresh");

Console.WriteLine("Travel start gate: 16 checks passed.");
