namespace Platform.Entity.Models;

/// <summary>Why a configuration write was accepted or refused.</summary>
public enum SetConfigurationOutcome
{
    Ok = 0,

    /// <summary>No such key. Keys are seed data and cannot be created by a caller.</summary>
    UnknownKey = 1,

    /// <summary>
    /// A one-time key, and the branch has already traded. Changing it now would
    /// leave documents computed one way beside documents computed another.
    /// </summary>
    Locked = 2,
}
