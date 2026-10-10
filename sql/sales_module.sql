-- ============================================================
-- Sales Module (Πωλητές): Επισκέψεις σε γιατρούς + Εξοδολόγιο
-- Τρέχει ΠΡΙΝ το deploy της έκδοσης που το χρησιμοποιεί
-- (ίδιο pattern με issues_module.sql: χωρίς FK constraints).
-- ============================================================

-- Ελληνικό όνομα γιατρού: τα ονόματα στον Doctors είναι greeklish,
-- οι πωλητές δουλεύουν με τα ελληνικά. Συμπληρώνεται από το import
-- των παλιών επισκέψεων και από τη φόρμα γιατρών αργότερα.
ALTER TABLE Doctors ADD COLUMN FullNameGreek VARCHAR(255) NULL AFTER FullName;

-- Επισκέψεις πωλητών. Ορατές σε ΟΛΟΥΣ τους χρήστες (για να βλέπει ο ένας
-- πού πήγε ο άλλος) -- μόνο η διαγραφή περιορίζεται σε δικές του/admin.
-- DoctorID NULL = γιατρός που δεν υπάρχει (ακόμα) στον πίνακα Doctors·
-- το DoctorName είναι πάντα συμπληρωμένο (ελληνικό snapshot).
CREATE TABLE SalesVisits (
    VisitID     BIGINT       NOT NULL AUTO_INCREMENT,
    UserID      INT          NOT NULL,              -- πωλητής (Users.IdUser)
    DoctorID    INT          NULL,                  -- Doctors.DoctorID (προαιρετικό)
    DoctorName  VARCHAR(255) NOT NULL,              -- ελληνικό όνομα (snapshot)
    VisitDate   DATE         NOT NULL,
    Notes       VARCHAR(500) NULL,
    CreatedAt   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (VisitID),
    KEY idx_visits_user_date (UserID, VisitDate),
    KEY idx_visits_doctor (DoctorID, VisitDate),
    KEY idx_visits_name (DoctorName),
    KEY idx_visits_date (VisitDate)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Εξοδολόγιο: μία γραμμή ανά ημέρα ανά πωλητή (όπως το Excel).
-- Ιδιωτικό: ο καθένας βλέπει μόνο τα δικά του, ο admin όλων.
CREATE TABLE SalesExpenses (
    ExpenseID   BIGINT        NOT NULL AUTO_INCREMENT,
    UserID      INT           NOT NULL,
    ExpenseDate DATE          NOT NULL,
    Destination VARCHAR(255)  NULL,
    Fuel        DECIMAL(10,2) NOT NULL DEFAULT 0,   -- Καύσιμα
    Tolls       DECIMAL(10,2) NOT NULL DEFAULT 0,   -- Διόδια
    Lodging     DECIMAL(10,2) NOT NULL DEFAULT 0,   -- Διαμονή
    Meals       DECIMAL(10,2) NOT NULL DEFAULT 0,   -- Γεύματα
    Parking     DECIMAL(10,2) NOT NULL DEFAULT 0,
    Tickets     DECIMAL(10,2) NOT NULL DEFAULT 0,   -- Εισιτήρια-Ταξί
    Misc        DECIMAL(10,2) NOT NULL DEFAULT 0,   -- Διάφορα
    CreatedAt   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (ExpenseID),
    UNIQUE KEY uq_expense_user_date (UserID, ExpenseDate)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Στοιχεία μήνα εξοδολογίου (κεφαλίδα του Excel: αρ. κυκλοφορίας, χιλιόμετρα).
CREATE TABLE SalesExpenseMonths (
    MonthID      INT         NOT NULL AUTO_INCREMENT,
    UserID       INT         NOT NULL,
    Year         INT         NOT NULL,
    Month        INT         NOT NULL,
    VehiclePlate VARCHAR(20) NULL,
    Kilometers   INT         NULL,
    PRIMARY KEY (MonthID),
    UNIQUE KEY uq_expmonth_user (UserID, Year, Month)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
