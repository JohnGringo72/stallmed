using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    // ---- Issues/Tasks Module (Εργασίες-Θέματα) ----
    // Πίνακες: sql/issues_module.sql. Ταυτότητα χρήστη: ο client στέλνει
    // UserID + όνομα στα requests (ίδιο pattern με τα υπόλοιπα modules).
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class IssuesController : ControllerBase
    {
        private readonly StallmedContext _context;

        public IssuesController(StallmedContext context)
        {
            _context = context;
        }

        // ---- Λίστα με προαιρετικά φίλτρα ----
        [HttpGet]
        public async Task<ActionResult<List<IssueListItemDto>>> GetIssues(
            [FromQuery] string? status, [FromQuery] string? search, [FromQuery] int? regardingUserId)
        {
            var query = _context.IssueTasks.AsQueryable();
            if (!string.IsNullOrEmpty(status))
                query = query.Where(i => i.Status == status);
            if (regardingUserId.HasValue)
                query = query.Where(i => i.RegardingUserID == regardingUserId);
            if (!string.IsNullOrEmpty(search))
                query = query.Where(i => i.Title.Contains(search)
                    || i.IssueCode.Contains(search)
                    || (i.RegardingName != null && i.RegardingName.Contains(search))
                    || (i.Description != null && i.Description.Contains(search)));

            var issues = await query.OrderByDescending(i => i.IssueID).Take(500).ToListAsync();

            var ids = issues.Select(i => i.IssueID).ToList();
            var commentCounts = await _context.IssueComments
                .Where(c => ids.Contains(c.IssueID))
                .GroupBy(c => c.IssueID)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            var attachmentCounts = await _context.IssueAttachments
                .Where(a => ids.Contains(a.IssueID))
                .GroupBy(a => a.IssueID)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            return Ok(issues.Select(i => new IssueListItemDto
            {
                IssueID = i.IssueID,
                IssueCode = i.IssueCode,
                Title = i.Title,
                RegardingName = i.RegardingName,
                AssignedName = i.AssignedName,
                Status = i.Status,
                Priority = i.Priority,
                DueDate = i.DueDate,
                CreatedByName = i.CreatedByName,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt,
                CommentCount = commentCounts.TryGetValue(i.IssueID, out var cc) ? cc : 0,
                AttachmentCount = attachmentCounts.TryGetValue(i.IssueID, out var ac) ? ac : 0
            }).ToList());
        }

        // ---- Λεπτομέρειες: θέμα + συνομιλία + συνημμένα ----
        [HttpGet("{id:long}")]
        public async Task<ActionResult<IssueDetailsDto>> GetIssue(long id)
        {
            var issue = await _context.IssueTasks.FindAsync(id);
            if (issue == null) return NotFound();

            var comments = await _context.IssueComments
                .Where(c => c.IssueID == id)
                .OrderBy(c => c.CommentID)
                .Select(c => new IssueCommentDto
                {
                    CommentID = c.CommentID,
                    UserID = c.UserID,
                    UserName = c.UserName,
                    CommentText = c.CommentText,
                    CreatedAt = c.CreatedAt
                }).ToListAsync();

            var attachments = await _context.IssueAttachments
                .Where(a => a.IssueID == id)
                .OrderBy(a => a.AttachmentID)
                .Select(a => new IssueAttachmentDto
                {
                    AttachmentID = a.AttachmentID,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    UploadedByName = a.UploadedByName,
                    CreatedAt = a.CreatedAt
                }).ToListAsync();

            return Ok(new IssueDetailsDto { Issue = issue, Comments = comments, Attachments = attachments });
        }

        // ---- Δημιουργία / Ενημέρωση ----
        [HttpPost("save")]
        public async Task<ActionResult<IssueSaveResult>> SaveIssue([FromBody] SaveIssueRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Title))
                return Ok(new IssueSaveResult { Success = false, Message = "Ο τίτλος είναι υποχρεωτικός." });

            IssueTask issue;
            if (req.IssueID.HasValue)
            {
                issue = await _context.IssueTasks.FindAsync(req.IssueID.Value);
                if (issue == null) return NotFound();
            }
            else
            {
                // Αν ο client δεν έστειλε όνομα, βρίσκεται από τη βάση ώστε το
                // «Ποιος το άνοιξε» να συμπληρώνεται πάντα αυτόματα.
                var actingName = req.ActingUserName;
                if (string.IsNullOrWhiteSpace(actingName) && req.ActingUserID.HasValue)
                {
                    actingName = await _context.Users
                        .Where(u => u.IdUser == req.ActingUserID.Value)
                        .Select(u => (u.Firstname + " " + u.Lastname).Trim())
                        .FirstOrDefaultAsync();
                }
                issue = new IssueTask
                {
                    CreatedBy = req.ActingUserID,
                    CreatedByName = actingName,
                    CreatedAt = DateTime.Now
                };
                _context.IssueTasks.Add(issue);
            }

            issue.Title = req.Title.Trim();
            issue.Description = req.Description;
            issue.RegardingUserID = req.RegardingUserID;
            issue.RegardingName = req.RegardingName;
            issue.AssignedUserID = req.AssignedUserID;
            issue.AssignedName = req.AssignedName;
            issue.Status = string.IsNullOrEmpty(req.Status) ? "Open" : req.Status;
            issue.Priority = string.IsNullOrEmpty(req.Priority) ? "Normal" : req.Priority;
            issue.DueDate = req.DueDate;
            issue.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // Ο κωδικός βγαίνει από το IssueID, άρα συμπληρώνεται μετά το πρώτο save.
            if (string.IsNullOrEmpty(issue.IssueCode))
            {
                issue.IssueCode = $"TSK-{issue.IssueID:0000}";
                await _context.SaveChangesAsync();
            }

            return Ok(new IssueSaveResult { Success = true, IssueID = issue.IssueID, IssueCode = issue.IssueCode });
        }

        // ---- Σχόλια (συνομιλία) ----
        [HttpPost("comments")]
        public async Task<ActionResult<IssueSaveResult>> AddComment([FromBody] AddIssueCommentRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.CommentText))
                return Ok(new IssueSaveResult { Success = false, Message = "Κενό σχόλιο." });

            var issue = await _context.IssueTasks.FindAsync(req.IssueID);
            if (issue == null) return NotFound();

            _context.IssueComments.Add(new IssueComment
            {
                IssueID = req.IssueID,
                UserID = req.UserID,
                UserName = req.UserName,
                CommentText = req.CommentText.Trim(),
                CreatedAt = DateTime.Now
            });
            issue.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new IssueSaveResult { Success = true, IssueID = req.IssueID });
        }

        // ---- Συνημμένα (ίδιο pattern με QuoteAttachments) ----
        [HttpPost("attachments/{issueId:long}")]
        public async Task<ActionResult> UploadAttachment(long issueId, IFormFile file,
            [FromQuery] int? userId, [FromQuery] string? userName)
        {
            var issue = await _context.IssueTasks.FindAsync(issueId);
            if (issue == null) return NotFound();
            if (file == null || file.Length == 0) return BadRequest("Κενό αρχείο.");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            _context.IssueAttachments.Add(new IssueAttachment
            {
                IssueID = issueId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileData = ms.ToArray(),
                UploadedBy = userId,
                UploadedByName = userName,
                CreatedAt = DateTime.Now
            });
            issue.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { Success = true });
        }

        [HttpGet("attachments/file/{attachmentId:long}")]
        public async Task<ActionResult> DownloadAttachment(long attachmentId)
        {
            var att = await _context.IssueAttachments.FindAsync(attachmentId);
            if (att == null) return NotFound();
            return File(att.FileData, att.ContentType ?? "application/octet-stream", att.FileName ?? "attachment");
        }

        [HttpPost("attachments/delete/{attachmentId:long}")]
        public async Task<ActionResult> DeleteAttachment(long attachmentId)
        {
            var att = await _context.IssueAttachments.FindAsync(attachmentId);
            if (att == null) return NotFound();
            _context.IssueAttachments.Remove(att);
            await _context.SaveChangesAsync();
            return Ok(new { Success = true });
        }

        // ---- Ενεργοί χρήστες για τα dropdowns (Αφορά / Ανάθεση) ----
        [HttpGet("users")]
        public async Task<ActionResult<List<IssueUserDto>>> GetUsers()
        {
            var users = await _context.Users
                .Where(u => u.Active)
                .OrderBy(u => u.Lastname).ThenBy(u => u.Firstname)
                .Select(u => new IssueUserDto
                {
                    IdUser = u.IdUser,
                    FullName = (u.Firstname + " " + u.Lastname).Trim(),
                    Role = u.Role
                }).ToListAsync();
            return Ok(users);
        }

        // ---- Ενεργοί γιατροί για το dropdown «Αφορά» (όλοι, χωρίς Take(50)
        // όπως στο PrickDoctorOrder, γιατί εδώ δεν υπάρχει search-as-you-type) ----
        [HttpGet("doctors")]
        public async Task<ActionResult<List<DoctorOptionDto>>> GetDoctors()
        {
            var list = await _context.Doctors
                .Where(d => d.IsActive)
                .OrderBy(d => d.FullName)
                .Select(d => new DoctorOptionDto { DoctorID = d.DoctorID, FullName = d.FullName })
                .ToListAsync();
            return Ok(list);
        }
    }
}
