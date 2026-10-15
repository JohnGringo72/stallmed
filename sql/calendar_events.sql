-- =====================================================================
-- Ημερολόγιο (μέσα στη σελίδα Todo): γεγονότα όπως γιορτές, αργίες, εκπαιδεύσεις.
-- Ποιος το βλέπει (VisibleTo):
--   'Me'   = μόνο αυτός που το έφτιαξε (προεπιλογή)
--   'All'  = όλοι
--   'Team' = μέλη της ομάδας VisibleTeam (SALES / SM / BM / TECO)
--   'User' = ο χρήστης VisibleUserID (και ο δημιουργός)
-- Company: SM / BM / SMBM / NULL -- φαίνεται μόνο σε admin.
-- Τρέξε το μία φορά στη βάση OnlineData, ΠΡΙΝ ανέβει η νέα έκδοση.
-- =====================================================================

CREATE TABLE IF NOT EXISTS CalendarEvents (
    EventID        BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    Title          VARCHAR(200)    NOT NULL,
    Notes          TEXT            NULL,
    StartDate      DATE            NOT NULL,
    EndDate        DATE            NULL,              -- NULL = μία ημέρα
    Company        VARCHAR(10)     NULL,
    VisibleTo      VARCHAR(10)     NOT NULL DEFAULT 'Me',
    VisibleTeam    VARCHAR(10)     NULL,
    VisibleUserID  INT             NULL,
    CreatedBy      INT             NOT NULL,
    CreatedByName  VARCHAR(200)    NULL,
    CreatedAt      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (EventID),
    KEY idx_calendar_dates (StartDate, EndDate),
    KEY idx_calendar_creator (CreatedBy)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
