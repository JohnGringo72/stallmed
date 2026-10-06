using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    public class IssuesService
    {
        private readonly DataService dataService;

        public IssuesService(DataService dataService)
        {
            this.dataService = dataService;
        }

        public async Task<List<IssueListItemDto>> GetIssues(string? status, string? search, int? userId = null,
                                                           bool includeArchived = false)
        {
            var qs = new List<string>();
            if (!string.IsNullOrEmpty(status)) qs.Add($"status={Uri.EscapeDataString(status)}");
            if (!string.IsNullOrEmpty(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
            if (userId.HasValue) qs.Add($"userId={userId.Value}");
            if (includeArchived) qs.Add("includeArchived=true");
            var query = qs.Count > 0 ? "?" + string.Join("&", qs) : "";
            return await dataService.Get<List<IssueListItemDto>>($"api/issues{query}") ?? new();
        }

        public async Task<IssueDetailsDto?> GetIssue(long id, int? userId = null)
            => await dataService.Get<IssueDetailsDto>(
                $"api/issues/{id}" + (userId.HasValue ? $"?userId={userId.Value}" : ""));

        public async Task<IssueSaveResult?> SaveIssue(SaveIssueRequest req)
            => await dataService.Post<SaveIssueRequest, IssueSaveResult>("api/issues/save", req);

        public async Task<IssueSaveResult?> AddComment(AddIssueCommentRequest req)
            => await dataService.Post<AddIssueCommentRequest, IssueSaveResult>("api/issues/comments", req);

        public async Task<IssueSaveResult?> DeleteComment(DeleteIssueCommentRequest req)
            => await dataService.Post<DeleteIssueCommentRequest, IssueSaveResult>("api/issues/comments/delete", req);

        public async Task<bool> UploadAttachment(long issueId, byte[] fileBytes, string fileName, int? userId, string? userName)
        {
            var qs = $"?userId={userId}&userName={Uri.EscapeDataString(userName ?? "")}";
            var result = await dataService.PostFile<object>($"api/issues/attachments/{issueId}{qs}", fileBytes, fileName);
            return result != null;
        }

        public async Task<byte[]> DownloadAttachment(long attachmentId)
            => await dataService.GetBytes($"api/issues/attachments/file/{attachmentId}");

        public async Task DeleteAttachment(long attachmentId)
            => await dataService.Post<object, object>($"api/issues/attachments/delete/{attachmentId}", new { });

        public async Task<IssueSaveResult?> SetArchived(long issueId, bool archived, int userId)
            => await dataService.Post<SetIssueArchivedRequest, IssueSaveResult>("api/issues/set-archived",
                new SetIssueArchivedRequest { IssueID = issueId, Archived = archived, UserID = userId });

        public async Task MarkRead(long issueId, int userId)
            => await dataService.Post<MarkIssueReadRequest, object>("api/issues/mark-read",
                new MarkIssueReadRequest { IssueID = issueId, UserID = userId });

        public async Task<IssueMySummaryDto?> GetMySummary(int userId)
            => await dataService.Get<IssueMySummaryDto>($"api/issues/my-summary?userId={userId}");

        public async Task<IssueSaveResult?> DeleteIssue(long issueId, int userId)
            => await dataService.Post<object, IssueSaveResult>($"api/issues/delete/{issueId}?userId={userId}", new { });

        public async Task<List<IssueUserDto>> GetUsers()
            => await dataService.Get<List<IssueUserDto>>("api/issues/users") ?? new();

        public async Task<List<DoctorOptionDto>> GetDoctors()
            => await dataService.Get<List<DoctorOptionDto>>("api/issues/doctors") ?? new();
    }
}
