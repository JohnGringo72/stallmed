-- =====================================================================
-- Issues / Tasks Module (Εργασίες-Θέματα) -- MySQL schema
-- Τρέξε το script μία φορά στη βάση OnlineData.
-- Χρήστες: αναφορές στο Users.IdUser χωρίς FK constraint (ίδιο pattern
-- με Quotes.CreatedBy) + snapshot ονόματος, ώστε το ιστορικό να μένει
-- ευανάγνωστο ακόμα κι αν αλλάξει/απενεργοποιηθεί ο χρήστης.
-- =====================================================================

-- 1. Κεφαλίδες θεμάτων
CREATE TABLE IF NOT EXISTS IssueTasks (
    IssueID          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    IssueCode        VARCHAR(20)     NOT NULL DEFAULT '',   -- TSK-0001, συμπληρώνεται μετά το insert
    Title            VARCHAR(255)    NOT NULL,
    Description      TEXT            NULL,
    RegardingUserID  INT             NULL,                  -- ποιον αφορά (πωλητής/υπάλληλος από Users)
    RegardingName    VARCHAR(255)    NULL,                  -- snapshot ονόματος ή ελεύθερο κείμενο
    AssignedUserID   INT             NULL,                  -- σε ποιον ανατέθηκε
    AssignedName     VARCHAR(255)    NULL,
    Status           VARCHAR(20)     NOT NULL DEFAULT 'Open',    -- Open / InProgress / Done / Cancelled
    Priority         VARCHAR(10)     NOT NULL DEFAULT 'Normal',  -- Low / Normal / High
    DueDate          DATE            NULL,
    CreatedBy        INT             NULL,                  -- ποιος το άνοιξε
    CreatedByName    VARCHAR(255)    NULL,
    CreatedAt        DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt        DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (IssueID),
    KEY idx_issues_status (Status),
    KEY idx_issues_regarding (RegardingUserID),
    KEY idx_issues_created (CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. Συνομιλία (σχόλια) ανά θέμα
CREATE TABLE IF NOT EXISTS IssueComments (
    CommentID    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    IssueID      BIGINT UNSIGNED NOT NULL,
    UserID       INT             NULL,
    UserName     VARCHAR(255)    NULL,
    CommentText  TEXT            NOT NULL,
    CreatedAt    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (CommentID),
    KEY idx_issuecomments_issue (IssueID),
    CONSTRAINT fk_issuecomments_issue FOREIGN KEY (IssueID) REFERENCES IssueTasks (IssueID)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3. Συνημμένα ανά θέμα (π.χ. συνταγές) -- ίδιο pattern με QuoteAttachments
CREATE TABLE IF NOT EXISTS IssueAttachments (
    AttachmentID  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    IssueID       BIGINT UNSIGNED NOT NULL,
    FileName      VARCHAR(255)    NULL,
    ContentType   VARCHAR(100)    NULL,
    FileData      LONGBLOB        NOT NULL,
    UploadedBy    INT             NULL,
    UploadedByName VARCHAR(255)   NULL,
    CreatedAt     DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (AttachmentID),
    KEY idx_issueattachments_issue (IssueID),
    CONSTRAINT fk_issueattachments_issue FOREIGN KEY (IssueID) REFERENCES IssueTasks (IssueID)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
