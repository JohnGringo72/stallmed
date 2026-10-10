-- ============================================================
-- Περιοχή γιατρού για τους πωλητές: "ΑΘΗΝΑ" ή "ΕΠΑΡΧΕΙΑ".
-- Τρέχει ΜΕΤΑ το sales_module.sql (ανεξάρτητο από το import).
-- ============================================================

ALTER TABLE Doctors ADD COLUMN Region VARCHAR(20) NULL AFTER City;

-- Αρχικό γέμισμα από την πόλη -- ΜΟΝΟ ως αφετηρία, επειδή οι πόλεις
-- έχουν λάθη: οι πωλητές τα διορθώνουν μετά από τη λίστα στατιστικών
-- (στήλη «Περιοχή» στο Visits -> «Πού πρέπει να πάμε»).
UPDATE Doctors SET Region = 'ΑΘΗΝΑ'
WHERE City IS NOT NULL AND UPPER(City) IN ('ATHENS', 'ATHINA', 'ΑΘΗΝΑ');

UPDATE Doctors SET Region = 'ΕΠΑΡΧΕΙΑ'
WHERE Region IS NULL AND City IS NOT NULL AND City <> '';
