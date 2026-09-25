namespace Student.Entity.Enums;

// The sis schema's fixed sets (S1, TK-61), stored by name.

public enum SubjectKind
{
    Core = 1,
    Language = 2,
    Elective = 3,
    CoCurricular = 4,
}

public enum Gender
{
    Male = 1,
    Female = 2,
    Other = 3,
    NotStated = 4,
}

/// <summary>Where a student stands. Anything but Active needs a leaving date.</summary>
public enum StudentStatus
{
    Active = 1,
    Alumni = 2,
    Withdrawn = 3,
    Transferred = 4,
}

public enum GuardianRelationship
{
    Father = 1,
    Mother = 2,
    Guardian = 3,
    Other = 4,
}

public enum EnrolmentStatus
{
    Active = 1,
    Promoted = 2,
    Detained = 3,
    Withdrawn = 4,
}

/// <summary>Marks are writable only while MarksOpen; Published shows them to parents.</summary>
public enum ExamStatus
{
    Planned = 1,
    MarksOpen = 2,
    Published = 3,
    Locked = 4,
}
