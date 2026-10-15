-- ============================================================
-- Tracking Module (Παρακολούθηση Συσκευών)
-- Τρέχει ΠΡΙΝ το deploy της έκδοσης που το χρησιμοποιεί.
--
-- ΠΡΟΣΟΧΗ: εδώ ΔΕΝ μπαίνουν δεδομένα GPS ούτε λίστα συσκευών.
-- Συσκευές, θέσεις, events, trips ζουν ΜΟΝΟ στον Traccar
-- (130.162.251.146:8082) και διαβάζονται live από το API του.
-- Εδώ κρατάμε μόνο ό,τι δεν ξέρει ο Traccar:
--   * οχήματα/assets (όχημα ≠ tracker),
--   * ποιο tracker/sensor ήταν σε ποιο όχημα και ΠΟΤΕ,
--   * ποιος χρήστης βλέπει τι,
--   * όρια θερμοκρασίας (cold-chain).
-- Οι συσκευές αναφέρονται με το Traccar devices.id (TraccarDeviceId).
-- Χωρίς FK constraints (ίδιο pattern με issues_module.sql).
--
-- Μελλοντικά (αποστολές TECO): Shipment → AssetID + χρονικό διάστημα
-- → μέσω TrackingAssetDevices βρίσκουμε GPS/θερμόμετρο εκείνης της
-- περιόδου → route/θερμοκρασίες από τον Traccar. Οδηγός: DeliveryPersons.
-- ============================================================

-- Όχημα / ψυγείο / container κλπ. Team = εταιρεία (SM / BM / TECO, όπως Users.Team).
CREATE TABLE TrackingAssets (
    AssetID     INT          NOT NULL AUTO_INCREMENT,
    Name        VARCHAR(150) NOT NULL,
    AssetType   VARCHAR(30)  NOT NULL DEFAULT 'Vehicle',   -- Vehicle / Fridge / Container / Other
    PlateNumber VARCHAR(30)  NULL,
    Team        VARCHAR(20)  NULL,
    Notes       VARCHAR(500) NULL,
    IsActive    TINYINT(1)   NOT NULL DEFAULT 1,
    CreatedAt   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (AssetID),
    KEY idx_assets_team (Team)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Σύνδεση όχημα ↔ συσκευή Traccar με χρονικό διάστημα (UTC).
-- Αλλαγή tracker = κλείσιμο ValidToUtc της παλιάς γραμμής + νέα γραμμή:
-- το ιστορικό του οχήματος συνεχίζει να βρίσκεται.
-- DeviceRole: GPS / TEMP (θερμόμετρο) / OTHER.
CREATE TABLE TrackingAssetDevices (
    LinkID          INT         NOT NULL AUTO_INCREMENT,
    AssetID         INT         NOT NULL,
    TraccarDeviceId INT         NOT NULL,
    DeviceRole      VARCHAR(20) NOT NULL DEFAULT 'GPS',
    ValidFromUtc    DATETIME    NOT NULL,
    ValidToUtc      DATETIME    NULL,                     -- NULL = ισχύει ακόμα
    CreatedAt       DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (LinkID),
    KEY idx_links_asset (AssetID, ValidFromUtc),
    KEY idx_links_device (TraccarDeviceId, ValidFromUtc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Ποιος χρήστης βλέπει τι. Οι admin του site βλέπουν τα πάντα χωρίς γραμμή εδώ.
--   Technical    : όλες οι συσκευές + Raw Data (Team/AssetID αγνοούνται)
--   CompanyAdmin : όλα τα οχήματα της εταιρείας Team
--   User         : μόνο το όχημα AssetID (μία γραμμή ανά όχημα)
CREATE TABLE TrackingUserAccess (
    AccessID    INT         NOT NULL AUTO_INCREMENT,
    UserID      INT         NOT NULL,                     -- Users.IdUser
    AccessLevel VARCHAR(20) NOT NULL,
    Team        VARCHAR(20) NULL,
    AssetID     INT         NULL,
    CreatedAt   DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (AccessID),
    KEY idx_access_user (UserID)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Επιτρεπτά όρια θερμοκρασίας. Ισχύει το πιο συγκεκριμένο:
-- Device > Asset > Default. ShipmentID για το μελλοντικό module αποστολών.
CREATE TABLE TrackingTempRanges (
    RangeID         INT          NOT NULL AUTO_INCREMENT,
    Scope           VARCHAR(20)  NOT NULL,                -- Default / Asset / Device / Shipment
    AssetID         INT          NULL,
    TraccarDeviceId INT          NULL,
    ShipmentID      INT          NULL,
    MinC            DECIMAL(6,2) NOT NULL,
    MaxC            DECIMAL(6,2) NOT NULL,
    CreatedAt       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (RangeID),
    KEY idx_ranges_scope (Scope, AssetID, TraccarDeviceId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Προεπιλογή cold-chain: +2 έως +8 °C (αλλάζει από τη σελίδα, όχι από τον κώδικα).
INSERT INTO TrackingTempRanges (Scope, MinC, MaxC) VALUES ('Default', 2.00, 8.00);
