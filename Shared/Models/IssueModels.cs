using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StallmedManager.Shared.Models
{
    // ---- Issues / Tasks Module (Εργασίες-Θέματα) ----
    // Πίνακες: sql/issues_module.sql (χωρίς EF migrations, όπως όλο το project).

    [Table("IssueTasks")]
    public class IssueTask
    {
        [Key]
        public long IssueID { get; set; }
        public string IssueCode { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public int? RegardingUserID { get; set; }
        public string? RegardingName { get; set; }
        public int? AssignedUserID { get; set; }
        public string? AssignedName { get; set; }
        public string Status { get; set; } = "Open";
        public string Priority { get; set; } = "Normal";
        public DateTime? DueDate { get; set; }
        public int? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [Table("IssueComments")]
    public class IssueComment
    {
        [Key]
        public long CommentID { get; set; }
        public long IssueID { get; set; }
        public int? UserID { get; set; }
        public string? UserName { get; set; }
        public string CommentText { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    [Table("IssueAttachments")]
    public class IssueAttachment
    {
        [Key]
        public long AttachmentID { get; set; }
        public long IssueID { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public byte[] FileData { get; set; } = Array.Empty<byte>();
        public int? UploadedBy { get; set; }
        public string? UploadedByName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ---- DTOs ----

    public class IssueListItemDto
    {
        public long IssueID { get; set; }
        public string IssueCode { get; set; } = "";
        public string Title { get; set; } = "";
        public string? RegardingName { get; set; }
        public string? AssignedName { get; set; }
        public string Status { get; set; } = "Open";
        public string Priority { get; set; } = "Normal";
        public DateTime? DueDate { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int CommentCount { get; set; }
        public int AttachmentCount { get; set; }
    }

    public class IssueDetailsDto
    {
        public IssueTask Issue { get; set; } = new();
        public List<IssueCommentDto> Comments { get; set; } = new();
        public List<IssueAttachmentDto> Attachments { get; set; } = new();
    }

    public class IssueCommentDto
    {
        public long CommentID { get; set; }
        public int? UserID { get; set; }
        public string? UserName { get; set; }
        public string CommentText { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class IssueAttachmentDto
    {
        public long AttachmentID { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public string? UploadedByName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SaveIssueRequest
    {
        public long? IssueID { get; set; }           // null = νέο θέμα
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public int? RegardingUserID { get; set; }
        public string? RegardingName { get; set; }
        public int? AssignedUserID { get; set; }
        public string? AssignedName { get; set; }
        public string Status { get; set; } = "Open";
        public string Priority { get; set; } = "Normal";
        public DateTime? DueDate { get; set; }
        public int? ActingUserID { get; set; }       // ποιος κάνει την ενέργεια
        public string? ActingUserName { get; set; }
    }

    public class AddIssueCommentRequest
    {
        public long IssueID { get; set; }
        public int? UserID { get; set; }
        public string? UserName { get; set; }
        public string CommentText { get; set; } = "";
    }

    public class IssueUserDto
    {
        public int IdUser { get; set; }
        public string FullName { get; set; } = "";
        public string? Role { get; set; }
    }

    public class IssueSaveResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public long IssueID { get; set; }
        public string? IssueCode { get; set; }
    }
}
