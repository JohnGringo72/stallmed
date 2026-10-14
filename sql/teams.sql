-- ============================================================
-- Ομάδες χρηστών και ανάθεση θέματος σε ομάδα
-- ΤΡΕΞΕ ΑΥΤΟ ΠΡΙΝ ΑΝΕΒΑΣΕΙΣ ΤΗ ΝΕΑ ΕΚΔΟΣΗ.
-- Χωρίς τις στήλες, η λίστα θεμάτων και η σελίδα χρηστών θα σκάσουν.
-- ============================================================

-- Σε ποια ομάδα ανήκει ο χρήστης. Κενό = καμία (π.χ. ο admin).
-- Τιμές: SALES, SM, BM, TECO
ALTER TABLE Users
    ADD COLUMN Team VARCHAR(20) NULL;

-- Σε ποια ομάδα απευθύνεται το θέμα. Κενό = δεν απευθύνεται σε ομάδα.
ALTER TABLE IssueTasks
    ADD COLUMN AssignedTeam VARCHAR(20) NULL;

-- Έλεγχος ότι μπήκαν και οι δύο (πρέπει να γυρίσει 2 γραμμές)
SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'OnlineData'
  AND ((TABLE_NAME = 'Users' AND COLUMN_NAME = 'Team')
    OR (TABLE_NAME = 'IssueTasks' AND COLUMN_NAME = 'AssignedTeam'));

-- Τις ομάδες των χρηστών θα τις ορίσεις από ADMIN -> Χρήστες.
-- Αν προτιμάς να τις βάλεις μαζικά εδώ, κάτι σαν:
-- UPDATE Users SET Team = 'SM'    WHERE Username IN ('...', '...');
-- UPDATE Users SET Team = 'BM'    WHERE Username IN ('...');
-- UPDATE Users SET Team = 'TECO'  WHERE Username IN ('TecoDianomes');
-- UPDATE Users SET Team = 'SALES' WHERE Username IN ('...');
