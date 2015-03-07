using System;

namespace OpenOcdTclClient
{
    public enum TargetEvent
    {
        GdbHalt,
        Halted,
        Resumed,
        ResumeStart,
        ResumeEnd,
        GdbStart,
        GdbEnd,
        ResetStart,
        ResetAssertPre,
        ResetAssert,
        ResetAssertPost,
        ResetDeassertPre,
        ResetDeassertPost,
        ResetHaltPre,
        ResetHaltPost,
        ResetWaitPre,
        ResetWaitPost,
        ResetInit,
        ResetEnd,
        DebugHalted,
        DebugResumed,
        ExamineStart,
        ExamineEnd,
        GdbAttach,
        GdbDetach,
        GdbFlashEraseStart,
        GdbFlashEraseEnd,
        GdbFlashWriteStart,
        GdbFlashWriteEnd,
        TraceConfig,
        Unknown
    }

    public enum TargetState
    {
        Unknown,
        Running,
        Halted,
        Reset,
        DebugRunning
    }

    public enum TargetResetMode
    {
        Unknown,
        Run,
        Halt,
        Init
    }
}
