-- =====================================================================
-- Διανομές σε γιατρούς (σελίδα «🎁 Διανομές»): ημερολόγια, δώρα, υλικό.
-- Ποιος γιατρός πήρε τι, πόσα, πότε και ποιος το έδωσε.
-- Τρέξε το μία φορά στη βάση OnlineData, ΠΡΙΝ ανέβει η νέα έκδοση.
-- =====================================================================

CREATE TABLE IF NOT EXISTS Distributions (
    DistributionID  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    Item            VARCHAR(100)    NOT NULL,          -- π.χ. 'Ημερολόγιο 2027'
    DoctorID        INT             NULL,              -- Doctors.DoctorID (NULL αν γράφτηκε ελεύθερο όνομα)
    DoctorName      VARCHAR(200)    NOT NULL,
    Quantity        INT             NOT NULL DEFAULT 1,
    GivenDate       DATE            NOT NULL,
    GivenByUserID   INT             NOT NULL,          -- ποιος το έδωσε
    GivenByName     VARCHAR(200)    NULL,
    Notes           VARCHAR(500)    NULL,
    CreatedBy       INT             NOT NULL,          -- ποιος το καταχώρησε
    CreatedAt       DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (DistributionID),
    KEY idx_distributions_doctor (DoctorID),
    KEY idx_distributions_item (Item, GivenDate)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
