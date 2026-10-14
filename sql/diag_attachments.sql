-- ============================================================
-- Γιατί δεν ανοίγουν τα συνημμένα -- ΜΟΝΟ SELECT
-- ============================================================

-- 1) ΤΟ ΚΡΙΣΙΜΟ: τι τύπος είναι η στήλη που κρατάει το αρχείο.
--    blob        = 64 KB  -> σχεδόν κάθε φωτογραφία κόβεται
--    mediumblob  = 16 MB
--    longblob    = 4 GB   (αυτό θέλουμε)
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'OnlineData'
  AND TABLE_NAME = 'IssueAttachments'
ORDER BY ORDINAL_POSITION;


-- 2) Πόσα bytes έχει πραγματικά κάθε συνημμένο.
--    Bytes = 0 ή NULL -> το αρχείο δεν αποθηκεύτηκε ποτέ.
SELECT AttachmentID,
       IssueID,
       FileName,
       ContentType,
       OCTET_LENGTH(FileData) AS Bytes,
       UploadedByName,
       CreatedAt
FROM OnlineData.IssueAttachments
ORDER BY AttachmentID DESC
LIMIT 30;


-- 3) Σύνοψη: πόσα είναι άδεια και πόσα έχουν περιεχόμενο.
SELECT CASE
           WHEN FileData IS NULL THEN 'NULL'
           WHEN OCTET_LENGTH(FileData) = 0 THEN 'άδειο'
           WHEN OCTET_LENGTH(FileData) >= 65535 THEN 'στο όριο 64KB (κομμένο;)'
           ELSE 'έχει περιεχόμενο'
       END AS Katastasi,
       COUNT(*) AS Plithos
FROM OnlineData.IssueAttachments
GROUP BY Katastasi;


-- 4) Αν το (1) δείξει blob/mediumblob, η διόρθωση είναι αυτή.
--    ΜΗΝ το τρέξεις πριν δω το αποτέλεσμα του (1).
-- ALTER TABLE IssueAttachments MODIFY FileData LONGBLOB NULL;
