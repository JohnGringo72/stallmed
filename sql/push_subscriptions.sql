-- =====================================================================
-- Ειδοποιήσεις Push (κινητό / desktop): μία γραμμή ανά συσκευή/browser.
-- Το Endpoint είναι η διεύθυνση της Google/Apple/Mozilla όπου στέλνουμε
-- την ειδοποίηση· P256dh/Auth είναι τα κλειδιά κρυπτογράφησής της.
-- Τρέξε το μία φορά στη βάση OnlineData, ΠΡΙΝ ανέβει η νέα έκδοση.
-- =====================================================================

CREATE TABLE IF NOT EXISTS PushSubscriptions (
    SubscriptionID BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    UserID         INT             NOT NULL,
    Endpoint       VARCHAR(700)    NOT NULL,
    P256dh         VARCHAR(200)    NOT NULL,
    Auth           VARCHAR(100)    NOT NULL,
    UserAgent      VARCHAR(400)    NULL,
    CreatedAt      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (SubscriptionID),
    UNIQUE KEY uq_pushsubs_endpoint (Endpoint),
    KEY idx_pushsubs_user (UserID)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
