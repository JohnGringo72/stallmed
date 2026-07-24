-- =====================================================================
-- Dashboard Αποθέματος (§8β) -- MySQL schema
-- Τρέξε το script μία φορά στη βάση OnlineData.
--
-- Το φυσικό/δεσμευμένο/παραγγελμένο απόθεμα ΔΕΝ αποθηκεύεται εδώ --
-- υπολογίζεται live από τα υπάρχοντα StockReceipts / DoctorOrderLines /
-- ProductionOrderLines. Το μόνο νέο δεδομένο είναι το όριο αναπαραγγελίας
-- (ReorderPoint) ανά κωδικό+τύπο προϊόντος.
-- =====================================================================

CREATE TABLE IF NOT EXISTS StockReorderPoints (
    ReorderPointID  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    CodePrick       VARCHAR(50)     NOT NULL,
    ProductTypeCode VARCHAR(50)     NOT NULL,
    ReorderPoint    INT             NOT NULL DEFAULT 0,
    UpdatedBy       INT             NULL,
    UpdatedAt       DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (ReorderPointID),
    UNIQUE KEY uq_reorderpoints_code_type (CodePrick, ProductTypeCode)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
