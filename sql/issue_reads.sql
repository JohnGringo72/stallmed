-- =====================================================================
-- Ανά χρήστη: ποια θέματα έχει ήδη ανοίξει.
-- Χωρίς αυτόν τον πίνακα, η ένδειξη "ΝΕΟ" θα ήταν κοινή για όλους --
-- θα έφευγε από όλους μόλις την άνοιγε ένας.
-- Τρέξε το μία φορά στη βάση OnlineData, ΠΡΙΝ ανέβει η νέα έκδοση.
-- =====================================================================

CREATE TABLE IF NOT EXISTS IssueReads (
    ReadID   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    IssueID  BIGINT UNSIGNED NOT NULL,
    UserID   INT             NOT NULL,
    ReadAt   DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (ReadID),
    UNIQUE KEY uq_issuereads_issue_user (IssueID, UserID),
    KEY idx_issuereads_user (UserID),
    CONSTRAINT fk_issuereads_issue FOREIGN KEY (IssueID) REFERENCES IssueTasks (IssueID)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
