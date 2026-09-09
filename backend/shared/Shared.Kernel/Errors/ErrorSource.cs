namespace Shared.Kernel.Errors;

/// <summary>Which part of the product raised the error. Decides who chases it.</summary>
public enum ErrorSource
{
    /// <summary>A request from a user or another service. Somebody saw a failure.</summary>
    Api = 0,

    /// <summary>A background worker. Nobody saw it, which is why it needs a follow-up status.</summary>
    Worker = 1,

    /// <summary>Startup — migration, seeding, provisioning.</summary>
    Startup = 2,
}

/// <summary>
/// Whether a recorded error still needs somebody to act.
///
/// This is the task list. A worker failure is written <see cref="Open"/>, and
/// the set of open rows is what an operator works through — an API failure a
/// user retried successfully does not need chasing, a costing run that gave up
/// does.
/// </summary>
public enum ErrorFollowUpStatus
{
    /// <summary>Recorded, nobody has looked. Where every worker error starts.</summary>
    Open = 0,

    /// <summary>Somebody has seen it and it is being dealt with.</summary>
    Acknowledged = 1,

    /// <summary>Dealt with.</summary>
    Resolved = 2,

    /// <summary>Looked at and deliberately closed — a caller's bad input needs no engineering follow-up.</summary>
    NoActionNeeded = 3,
}
