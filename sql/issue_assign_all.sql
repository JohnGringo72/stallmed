-- ============================================================
-- Ανάθεση θέματος «Προς όλους»
-- ΤΡΕΞΕ ΑΥΤΟ ΠΡΙΝ ΑΝΕΒΑΣΕΙΣ ΤΗ ΝΕΑ ΕΚΔΟΣΗ.
-- Χωρίς τη στήλη, η λίστα θεμάτων θα βγάζει σφάλμα.
-- ============================================================

-- Ρητή σήμανση ότι το θέμα αφορά όλους. Δεν το μπερδεύουμε με
-- κενή ανάθεση: κενό = παλιό θέμα πριν τον κανόνα, 1 = προς όλους.
ALTER TABLE IssueTasks
    ADD COLUMN AssignedToAll TINYINT(1) NOT NULL DEFAULT 0;

-- Έλεγχος ότι μπήκε
SELECT COLUMN_NAME, COLUMN_TYPE, COLUMN_DEFAULT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'OnlineData'
  AND TABLE_NAME = 'IssueTasks'
  AND COLUMN_NAME = 'AssignedToAll';
