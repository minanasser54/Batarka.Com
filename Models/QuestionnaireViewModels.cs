using System.ComponentModel.DataAnnotations;

namespace MyMvcApp10.Models;

public class QuestionnaireAnswerItemDto
{
    public int QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionCode { get; set; } = string.Empty;
    public bool AnswerValue { get; set; }
    public int PointsAwarded { get; set; }
}

public class QuestionnaireSubmissionListItemViewModel
{
    public int SubmissionId { get; set; }
    public int AttendanceRecordId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string? StudentUserName { get; set; }
    public string? StudentMobile { get; set; }
    public string? ClassName { get; set; }
    public string? LevelName { get; set; }
    public DateTime AttendanceDate { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public string? SubmittedByName { get; set; }
    public int TotalPointsAwarded { get; set; }
    public int TotalQuestionsCount { get; set; }
    public int AnsweredYesCount { get; set; }
    public string? CorrectionReason { get; set; }
    public string Status { get; set; } = "Submitted";
    public List<QuestionnaireAnswerItemDto> Answers { get; set; } = new();
}

public class QuestionnaireAnswersPageViewModel
{
    public List<QuestionnaireSubmissionListItemViewModel> Submissions { get; set; } = new();
    public List<AttendanceQuestionDefinition> AllQuestions { get; set; } = new();
    public int TotalSubmissionsCount { get; set; }
    public int TotalPointsAwarded { get; set; }
    public int UniqueStudentsCount { get; set; }
    public double PositiveAnswerPercentage { get; set; }
    public Dictionary<int, (string Name, int YesCount, int TotalCount)> QuestionStats { get; set; } = new();

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Search { get; set; }
    public int? SchoolLevelId { get; set; }
    public int? SchoolClassId { get; set; }
    public int? QuestionId { get; set; }
    public bool? AnswerFilter { get; set; }

    public List<SchoolLevel> Levels { get; set; } = new();
    public List<SchoolClass> Classes { get; set; } = new();
}

public class StudentContactDirectoryViewModel
{
    public List<AdminStudentListItemViewModel> Students { get; set; } = new();
    public List<SchoolLevel> Levels { get; set; } = new();
    public List<SchoolClass> Classes { get; set; } = new();
    public string? Search { get; set; }
    public int? SchoolLevelId { get; set; }
    public int? SchoolClassId { get; set; }
    public string? ActiveStatus { get; set; }
    public int TotalCount { get; set; }
    public int WithMobileCount { get; set; }
    public int WithAddressCount { get; set; }
}
