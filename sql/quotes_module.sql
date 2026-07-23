-- =====================================================================
-- Quotes Module (Προσφορές) -- MySQL schema
-- Τρέξε το script μία φορά στη βάση OnlineData.
-- Οι πελάτες/νοσοκομεία είναι εγγραφές του υπάρχοντος πίνακα Doctors
-- (τα ονόματα είναι ήδη περασμένα εκεί) -- προστίθενται μόνο τα πεδία
-- που λείπουν (ΑΦΜ, τμήμα, υπεύθυνος, ΤΚ).
-- =====================================================================

-- 1. Επέκταση του πίνακα Doctors με στοιχεία πελάτη-νοσοκομείου.
--    (Αν κάποια στήλη υπάρχει ήδη, η MySQL θα βγάλει σφάλμα "Duplicate
--    column name" -- αγνόησέ το για τη συγκεκριμένη στήλη και συνέχισε.)
ALTER TABLE Doctors ADD COLUMN VatNumber      VARCHAR(20)  NULL;
ALTER TABLE Doctors ADD COLUMN Department     VARCHAR(255) NULL;
ALTER TABLE Doctors ADD COLUMN ContactPerson  VARCHAR(255) NULL;
ALTER TABLE Doctors ADD COLUMN PostalCode     VARCHAR(20)  NULL;

-- 2. Κεφαλίδες προσφορών
-- Προσοχή: τα κλειδιά είναι UNSIGNED ώστε να ταιριάζουν με τα υπάρχοντα
-- Doctors.DoctorID (INT UNSIGNED) και DoctorOrders.OrderID (BIGINT UNSIGNED),
-- αλλιώς τα foreign keys απορρίπτονται (MySQL error #3780).
CREATE TABLE IF NOT EXISTS Quotes (
    QuoteID             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    QuoteNumber         VARCHAR(30)     NOT NULL,
    Company             VARCHAR(5)      NOT NULL,
    Status              VARCHAR(15)     NOT NULL DEFAULT 'Draft',
    IssueDate           DATE            NOT NULL,
    ValidUntil          DATE            NOT NULL,
    CustomerDoctorID    INT UNSIGNED    NULL,
    CustomerName        VARCHAR(255)    NULL,
    CustomerVat         VARCHAR(20)     NULL,
    CustomerDepartment  VARCHAR(255)    NULL,
    CustomerContact     VARCHAR(255)    NULL,
    CustomerEmail       VARCHAR(255)    NULL,
    CustomerPhone       VARCHAR(50)     NULL,
    HospitalRequestRef  VARCHAR(100)    NULL,
    Notes               TEXT            NULL,
    RejectReason        TEXT            NULL,
    SentAt              DATETIME        NULL,
    RespondedAt         DATETIME        NULL,
    ConvertedOrderID    BIGINT UNSIGNED NULL,
    Subtotal            DECIMAL(12,2)   NOT NULL DEFAULT 0,
    VatTotal            DECIMAL(12,2)   NOT NULL DEFAULT 0,
    Total               DECIMAL(12,2)   NOT NULL DEFAULT 0,
    TermsDelivery       VARCHAR(500)    NULL,
    TermsPayment        VARCHAR(500)    NULL,
    TermsWarranty       VARCHAR(500)    NULL,
    PdfPath             VARCHAR(500)    NULL,
    PdfData             LONGBLOB        NULL,
    CreatedBy           INT             NULL,
    CreatedAt           DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt           DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (QuoteID),
    UNIQUE KEY uq_quotes_number (QuoteNumber),
    KEY idx_quotes_status (Status),
    KEY idx_quotes_customer (CustomerDoctorID),
    KEY idx_quotes_issuedate (IssueDate),
    CONSTRAINT fk_quotes_doctor FOREIGN KEY (CustomerDoctorID) REFERENCES Doctors (DoctorID),
    CONSTRAINT fk_quotes_order FOREIGN KEY (ConvertedOrderID) REFERENCES DoctorOrders (OrderID)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3. Γραμμές προσφορών
CREATE TABLE IF NOT EXISTS QuoteLines (
    QuoteLineID     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    QuoteID         BIGINT UNSIGNED NOT NULL,
    CodePrick       VARCHAR(50)     NOT NULL,
    ProductTypeCode VARCHAR(50)     NOT NULL,
    Description     VARCHAR(500)    NULL,
    Quantity        INT             NOT NULL DEFAULT 1,
    Unit            VARCHAR(20)     NOT NULL DEFAULT 'τεμ.',
    UnitPrice       DECIMAL(12,2)   NOT NULL DEFAULT 0,
    DiscountPct     DECIMAL(5,2)    NOT NULL DEFAULT 0,
    VatRate         DECIMAL(5,2)    NOT NULL DEFAULT 0,
    LineNet         DECIMAL(12,2)   NOT NULL DEFAULT 0,
    LineVat         DECIMAL(12,2)   NOT NULL DEFAULT 0,
    LineTotal       DECIMAL(12,2)   NOT NULL DEFAULT 0,
    PRIMARY KEY (QuoteLineID),
    KEY idx_quotelines_quote (QuoteID),
    CONSTRAINT fk_quotelines_quote FOREIGN KEY (QuoteID) REFERENCES Quotes (QuoteID) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 4. Ιστορικό ενεργειών
CREATE TABLE IF NOT EXISTS QuoteEvents (
    QuoteEventID    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    QuoteID         BIGINT UNSIGNED NOT NULL,
    EventType       VARCHAR(30)     NOT NULL,
    Details         TEXT            NULL,
    CreatedBy       INT             NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (QuoteEventID),
    KEY idx_quoteevents_quote (QuoteID),
    CONSTRAINT fk_quoteevents_quote FOREIGN KEY (QuoteID) REFERENCES Quotes (QuoteID) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 5. Συνημμένα προσφορών (αρχεία ως blob, όπως τα DoctorOrderAttachments)
CREATE TABLE IF NOT EXISTS QuoteAttachments (
    AttachmentID    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    QuoteID         BIGINT UNSIGNED NOT NULL,
    FileName        VARCHAR(255)    NULL,
    ContentType     VARCHAR(100)    NULL,
    FileData        LONGBLOB        NOT NULL,
    UploadedBy      INT             NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (AttachmentID),
    KEY idx_quoteattachments_quote (QuoteID),
    CONSTRAINT fk_quoteattachments_quote FOREIGN KEY (QuoteID) REFERENCES Quotes (QuoteID) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
