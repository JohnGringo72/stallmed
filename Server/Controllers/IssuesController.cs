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
        private readonly StallmedManager.Server.Services.QuoteEmailService _email;
        private readonly ILogger<IssuesController> _logger;

        public IssuesController(StallmedContext context,
            StallmedManager.Server.Services.QuoteEmailService email,
            ILogger<IssuesController> logger)
        {
            _context = context;
            _email = email;
            _logger = logger;
        }

        // ---- Λίστα με προαιρετικά φίλτρα ----
        [HttpGet]
        public async Task<ActionResult<List<IssueListItemDto>>> GetIssues(
            [FromQuery] string? status, [FromQuery] string? search, [FromQuery] int? regardingUserId,
            [FromQuery] int? userId, [FromQuery] bool includeArchived = false)
        {
            var query = _context.IssueTasks.AsQueryable();
            // Τα αρχειοθετημένα δεν εμφανίζονται, εκτός αν ζητηθούν ρητά.
            if (!includeArchived)
                query = query.Where(i => i.ArchivedAt == null);
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

            // Τι έχει ήδη ανοίξει ΑΥΤΟΣ ο χρήστης. Χωρίς userId δεν σημαδεύεται τίποτα.
            // Αν λείπει ο πίνακας IssueReads (δεν έχει τρέξει ακόμα το sql/issue_reads.sql),
            // η λίστα πρέπει να εμφανίζεται κανονικά -- απλώς χωρίς την ένδειξη "νέο".
            var reads = new Dictionary<long, DateTime>();
            if (userId.HasValue)
            {
                try
                {
                    reads = await _context.IssueReads
                        .Where(r => r.UserID == userId.Value && ids.Contains(r.IssueID))
                        .ToDictionaryAsync(r => r.IssueID, r => r.ReadAt);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ο πίνακας IssueReads δεν είναι διαθέσιμος -- τρέξε το sql/issue_reads.sql");
                }
            }

            bool Unread(IssueTask i)
            {
                if (!userId.HasValue) return false;
                // Νέο = δεν το άνοιξε ποτέ, ή άλλαξε μετά την τελευταία φορά που το είδε.
                return !reads.TryGetValue(i.IssueID, out var readAt) || i.UpdatedAt > readAt;
            }

            return Ok(issues.Select(i => new IssueListItemDto
            {
                IssueID = i.IssueID,
                IssueCode = i.IssueCode,
                Title = i.Title,
                RegardingName = i.RegardingName,
                AssignedUserID = i.AssignedUserID,
                AssignedName = i.AssignedName,
                Status = i.Status,
                Priority = i.Priority,
                DueDate = i.DueDate,
                CreatedByName = i.CreatedByName,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt,
                CommentCount = commentCounts.TryGetValue(i.IssueID, out var cc) ? cc : 0,
                AttachmentCount = attachmentCounts.TryGetValue(i.IssueID, out var ac) ? ac : 0,
                IsUnread = Unread(i),
                IsArchived = i.ArchivedAt != null
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
            int? previousAssignee = null;
            if (req.IssueID.HasValue)
            {
                issue = await _context.IssueTasks.FindAsync(req.IssueID.Value);
                if (issue == null) return NotFound();
                previousAssignee = issue.AssignedUserID;
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

            // Ό,τι έγραψε ο ίδιος δεν πρέπει να του εμφανίζεται ως αδιάβαστο.
            if (req.ActingUserID.HasValue)
                await MarkReadInternal(issue.IssueID, req.ActingUserID.Value);

            // Ειδοποίηση μόνο όταν η ανάθεση είναι καινούργια ή άλλαξε, και ποτέ στον εαυτό σου.
            if (issue.AssignedUserID.HasValue
                && issue.AssignedUserID != previousAssignee
                && issue.AssignedUserID != req.ActingUserID)
            {
                await NotifyAssigneeAsync(issue);
            }

            return Ok(new IssueSaveResult { Success = true, IssueID = issue.IssueID, IssueCode = issue.IssueCode });
        }

        // Το email είναι best-effort: αν το SMTP δεν είναι ρυθμισμένο ή αποτύχει,
        // το θέμα έχει ήδη αποθηκευτεί -- δεν χαλάει η καταχώρηση.
        private async Task NotifyAssigneeAsync(IssueTask issue)
        {
            try
            {
                if (!_email.IsConfigured("SM"))
                {
                    _logger.LogInformation("Παράλειψη email ειδοποίησης για {Code}: το SMTP δεν είναι ρυθμισμένο.", issue.IssueCode);
                    return;
                }

                var user = await _context.Users
                    .Where(u => u.IdUser == issue.AssignedUserID!.Value && u.Active)
                    .Select(u => new { u.Email, Name = (u.Firstname + " " + u.Lastname).Trim() })
                    .FirstOrDefaultAsync();

                if (user == null || string.IsNullOrWhiteSpace(user.Email))
                {
                    _logger.LogInformation("Παράλειψη email ειδοποίησης για {Code}: ο χρήστης δεν έχει email.", issue.IssueCode);
                    return;
                }

                // Οι γραμμές ενώνονται με Environment.NewLine -- χωρίς escapes, για καθαρό κείμενο.
                var parts = new List<string>
                {
                    "Σου ανατέθηκε νέο θέμα.",
                    "",
                    $"Κωδικός: {issue.IssueCode}",
                    $"Τίτλος: {issue.Title}",
                    $"Προτεραιότητα: {issue.Priority}"
                };
                if (issue.DueDate.HasValue)
                    parts.Add($"Προθεσμία: {issue.DueDate:dd/MM/yyyy}");
                if (!string.IsNullOrWhiteSpace(issue.Description))
                {
                    parts.Add("");
                    parts.Add(issue.Description);
                }
                parts.Add("");
                parts.Add("Άνοιξε την εφαρμογή και δες το στα Tasks.");
                var body = string.Join(Environment.NewLine, parts);

                await _email.SendPlainAsync("SM", user.Email, user.Name,
                    $"[{issue.IssueCode}] {issue.Title}", body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Αποτυχία αποστολής email ειδοποίησης για το θέμα {Code}", issue.IssueCode);
            }
        }

        // ---- "Το είδα": καταγράφεται ανά χρήστη όταν ανοίγει το θέμα ----
        [HttpPost("mark-read")]
        public async Task<ActionResult> MarkRead([FromBody] MarkIssueReadRequest req)
        {
            if (req.UserID <= 0) return Ok();
            await MarkReadInternal(req.IssueID, req.UserID);
            return Ok();
        }

        private async Task MarkReadInternal(long issueId, int userId)
        {
            if (userId <= 0) return;

            try
            {
                await MarkReadCore(issueId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Δεν καταγράφηκε η ανάγνωση του θέματος {IssueID} -- τρέξε το sql/issue_reads.sql", issueId);

                // Η αποτυχημένη εγγραφή μένει στον change tracker και θα ξαναδοκιμαζόταν
                // στο επόμενο SaveChanges, ρίχνοντας ολόκληρο το αίτημα.
                foreach (var entry in _context.ChangeTracker.Entries<IssueRead>().ToList())
                    entry.State = EntityState.Detached;
            }
        }

        private async Task MarkReadCore(long issueId, int userId)
        {
            var existing = await _context.IssueReads
                .FirstOrDefaultAsync(r => r.IssueID == issueId && r.UserID == userId);

            if (existing == null)
                _context.IssueReads.Add(new IssueRead { IssueID = issueId, UserID = userId, ReadAt = DateTime.Now });
            else
                existing.ReadAt = DateTime.Now;

            await _context.SaveChangesAsync();
        }

        // ---- Σύνοψη για το καμπανάκι ----
        // Μετράει ΜΟΝΟ όσα αφορούν τον χρήστη και δεν τα έχει δει ακόμα:
        //   "με αφορά"  = ανατεθειμένο σε μένα, ή το άνοιξα εγώ, ή αφορά εμένα,
        //                 ή έχω σχολιάσει σε αυτό
        //   "αδιάβαστο" = δεν το άνοιξα ποτέ, ή άλλαξε μετά την τελευταία φορά
        // Έτσι το καμπανάκι σβήνει μόλις τα ανοίξει ο χρήστης, χωρίς να επηρεάζει τους άλλους.
        [HttpGet("my-summary")]
        public async Task<ActionResult<IssueMySummaryDto>> GetMySummary([FromQuery] int userId)
        {
            if (userId <= 0) return Ok(new IssueMySummaryDto());

            var commentedIssueIds = await _context.IssueComments
                .Where(c => c.UserID == userId)
                .Select(c => c.IssueID)
                .Distinct()
                .ToListAsync();

            var relevant = await _context.IssueTasks
                .Where(i => i.ArchivedAt == null)
                .Where(i => i.AssignedUserID == userId
                         || i.CreatedBy == userId
                         || i.RegardingUserID == userId
                         || commentedIssueIds.Contains(i.IssueID))
                .Select(i => new { i.IssueID, i.IssueCode, i.Title, i.UpdatedAt, i.Status, i.AssignedUserID })
                .ToListAsync();

            var ids = relevant.Select(x => x.IssueID).ToList();
            var reads = new Dictionary<long, DateTime>();
            try
            {
                reads = await _context.IssueReads
                    .Where(r => r.UserID == userId && ids.Contains(r.IssueID))
                    .ToDictionaryAsync(r => r.IssueID, r => r.ReadAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ο πίνακας IssueReads δεν είναι διαθέσιμος -- τρέξε το sql/issue_reads.sql");
                return Ok(new IssueMySummaryDto());   // καλύτερα κανένα σήμα, παρά σφάλμα
            }

            var unread = relevant
                .Where(i => !reads.TryGetValue(i.IssueID, out var readAt) || i.UpdatedAt > readAt)
                .OrderByDescending(i => i.UpdatedAt)     // πιο πρόσφατη κίνηση πρώτη
                .ThenByDescending(i => i.IssueID)
                .ToList();

            var newest = unread.FirstOrDefault();
            return Ok(new IssueMySummaryDto
            {
                UnreadCount = unread.Count,
                AssignedOpenCount = relevant.Count(i => i.AssignedUserID == userId
                                                     && i.Status != "Done" && i.Status != "Cancelled"),
                NewestIssueID = newest?.IssueID ?? 0,
                NewestIssueCode = newest?.IssueCode,
                NewestTitle = newest?.Title,
                NewestActivityAt = newest?.UpdatedAt,
                NewestIsNewIssue = newest != null && !reads.ContainsKey(newest.IssueID)
            });
        }

        // ---- Αρχειοθέτηση / επαναφορά ----
        // Δεν διαγράφει τίποτα: το θέμα απλώς φεύγει από τη λίστα και από το καμπανάκι.
        // Αντιστρέψιμο από οποιονδήποτε, σε αντίθεση με τη διαγραφή που είναι μόνο για admin.
        [HttpPost("set-archived")]
        public async Task<ActionResult<IssueSaveResult>> SetArchived([FromBody] SetIssueArchivedRequest req)
        {
            var issue = await _context.IssueTasks.FindAsync(req.IssueID);
            if (issue == null)
                return Ok(new IssueSaveResult { Success = false, Message = "Το θέμα δεν βρέθηκε." });

            issue.ArchivedAt = req.Archived ? DateTime.Now : null;
            issue.ArchivedBy = req.Archived ? req.UserID : null;
            // Το UpdatedAt ΔΕΝ αλλάζει: η αρχειοθέτηση δεν είναι αλλαγή που πρέπει
            // να εμφανιστεί ως "νέο" στους υπόλοιπους χρήστες.
            await _context.SaveChangesAsync();

            return Ok(new IssueSaveResult
            {
                Success = true,
                IssueID = issue.IssueID,
                Message = req.Archived
                    ? $"Το θέμα {issue.IssueCode} αρχειοθετήθηκε."
                    : $"Το θέμα {issue.IssueCode} επανήλθε στη λίστα."
            });
        }

        // ---- Διαγραφή θέματος (μόνο admin) ----
        // Τα IssueComments/IssueAttachments έχουν foreign key χωρίς ON DELETE CASCADE,
        // οπότε σβήνονται πρώτα αυτά και μετά το θέμα.
        [HttpPost("delete/{id:long}")]
        public async Task<ActionResult<IssueSaveResult>> DeleteIssue(long id, [FromQuery] int userId)
        {
            var role = await _context.Users.Where(u => u.IdUser == userId).Select(u => u.Role).FirstOrDefaultAsync();
            if (!string.Equals(role?.Trim(), "admin", StringComparison.OrdinalIgnoreCase))
                return Ok(new IssueSaveResult { Success = false, Message = "Μόνο ο admin μπορεί να διαγράψει θέμα." });

            var issue = await _context.IssueTasks.FindAsync(id);
            if (issue == null)
                return Ok(new IssueSaveResult { Success = false, Message = "Το θέμα δεν βρέθηκε." });

            var comments = await _context.IssueComments.Where(c => c.IssueID == id).ToListAsync();
            var attachments = await _context.IssueAttachments.Where(a => a.IssueID == id).ToListAsync();
            var reads = new List<IssueRead>();
            try { reads = await _context.IssueReads.Where(r => r.IssueID == id).ToListAsync(); }
            catch (Exception) { /* ο πίνακας μπορεί να μην υπάρχει ακόμα */ }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.IssueComments.RemoveRange(comments);
                _context.IssueAttachments.RemoveRange(attachments);
                _context.IssueReads.RemoveRange(reads);
                await _context.SaveChangesAsync();

                _context.IssueTasks.Remove(issue);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Αποτυχία διαγραφής θέματος {IssueID}", id);
                return Ok(new IssueSaveResult { Success = false, Message = "Η διαγραφή απέτυχε. Δοκίμασε ξανά." });
            }

            _logger.LogInformation("Ο χρήστης {UserID} διέγραψε το θέμα {Code} ({Comments} σχόλια, {Attachments} συνημμένα)",
                userId, issue.IssueCode, comments.Count, attachments.Count);

            return Ok(new IssueSaveResult
            {
                Success = true,
                IssueID = id,
                Message = $"Το θέμα {issue.IssueCode} διαγράφηκε."
            });
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

            // Το δικό σου σχόλιο δεν σε ειδοποιεί. Για τους υπόλοιπους που εμπλέκονται,
            // το θέμα γίνεται ξανά αδιάβαστο επειδή άλλαξε το UpdatedAt.
            if (req.UserID.HasValue)
                await MarkReadInternal(req.IssueID, req.UserID.Value);

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
