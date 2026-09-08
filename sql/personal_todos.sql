-- =====================================================================
-- Personal Todo (προσωπική λίστα εκκρεμοτήτων ανά χρήστη) -- MySQL
-- Τρέξε το script μία φορά στη βάση OnlineData.
-- Κάθε εγγραφή ανήκει σε έναν χρήστη (UserID) και εμφανίζεται ΜΟΝΟ σε αυτόν.
-- =====================================================================

CREATE TABLE IF NOT EXISTS PersonalTodos (
    TodoID     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    UserID     INT             NOT NULL,          -- ιδιοκτήτης (Users.IdUser)
    TodoText   VARCHAR(500)    NOT NULL,
    Notes      TEXT            NULL,
    Priority   VARCHAR(10)     NOT NULL DEFAULT 'Normal',  -- Low / Normal / High
    DueDate    DATE            NULL,
    IsDone     TINYINT(1)      NOT NULL DEFAULT 0,
    DoneAt     DATETIME        NULL,
    CreatedAt  DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt  DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (TodoID),
    KEY idx_personaltodos_user (UserID, IsDone)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
