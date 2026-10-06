-- ============================================================
-- Διαγνωστικά: γιατί το στοκ στα Μείγματα βγαίνει 0
-- ΜΟΝΟ SELECT -- δεν αλλάζει τίποτα στη βάση.
-- Τρέξε τα με τη σειρά και στείλε τα αποτελέσματα.
-- ============================================================

-- 1) Ποια ονόματα "ασθενή" μοιάζουν με την αποθήκη;
--    Δείχνει τους χαρακτήρες σε HEX για να ξεχωρίσουμε
--    λατινικό A (41) από ελληνικό Α (CE91 σε utf8).
SELECT Patient,
       HEX(Patient)      AS PatientHex,
       CHAR_LENGTH(Patient) AS Chars,
       COUNT(*)          AS Rows_,
       SUM(QNT)          AS QNT
FROM WebOrders
WHERE Patient IS NOT NULL
  AND CHAR_LENGTH(REPLACE(Patient, ' ', '')) <= 3
GROUP BY Patient
ORDER BY Rows_ DESC;


-- 2) Το στοκ της αποθήκης ανά είδος και κατάσταση.
--    Status 3 = Received (εδώ), Status 2 = Manufacturing (αναμένεται).
SELECT TreatmentDescription,
       Status,
       COUNT(*) AS Rows_,
       SUM(QNT) AS QNT
FROM WebOrders
WHERE REPLACE(Patient, ' ', '') IN ('AA', 'ΑΑ', 'AΑ', 'ΑA')
  AND Status IN ('2', '3')
GROUP BY TreatmentDescription, Status
ORDER BY QNT DESC
LIMIT 50;


-- 3) Σύγκριση ονομάτων: είδη με παραγγελίες ασθενών
--    δίπλα στα είδη που έχουν στοκ στην αποθήκη.
--    Αν μια γραμμή έχει Orders > 0 και Stock NULL, τα ονόματα ΔΕΝ ταιριάζουν.
SELECT COALESCE(o.TreatmentDescription, s.TreatmentDescription) AS Treatment,
       o.QNT  AS OrdersQNT,
       s.QNT  AS StockQNT
FROM (
    SELECT TreatmentDescription, SUM(QNT) AS QNT
    FROM WebOrders
    WHERE REPLACE(Patient, ' ', '') NOT IN ('AA', 'ΑΑ', 'AΑ', 'ΑA')
      AND Status <> '5'
    GROUP BY TreatmentDescription
) o
LEFT JOIN (
    SELECT TreatmentDescription, SUM(QNT) AS QNT
    FROM WebOrders
    WHERE REPLACE(Patient, ' ', '') IN ('AA', 'ΑΑ', 'AΑ', 'ΑA')
      AND Status IN ('2', '3')
    GROUP BY TreatmentDescription
) s ON s.TreatmentDescription = o.TreatmentDescription
ORDER BY o.QNT DESC
LIMIT 50;


-- 4) Στοκ ανά μείγμα (είδος + αλλεργιογόνο) -- έτσι το δείχνει η σελίδα.
SELECT TreatmentDescription,
       Allergen,
       SUM(CASE WHEN Status = '3' THEN QNT ELSE 0 END) AS Edo,
       SUM(CASE WHEN Status = '2' THEN QNT ELSE 0 END) AS Anamenetai,
       SUM(QNT) AS Synolo
FROM WebOrders
WHERE REPLACE(Patient, ' ', '') IN ('AA', 'ΑΑ', 'AΑ', 'ΑA')
  AND Status IN ('2', '3')
GROUP BY TreatmentDescription, Allergen
ORDER BY Synolo DESC
LIMIT 50;


-- 5) Αν το (1) δείξει άλλο όνομα αποθήκης (π.χ. "A.A." ή "ΑΠΟΘΗΚΗ"),
--    τρέξε αυτό βάζοντας το σωστό όνομα και στείλε το αποτέλεσμα.
-- SELECT TreatmentDescription, Allergen, Status, SUM(QNT) AS QNT
-- FROM WebOrders
-- WHERE Patient = 'ΒΑΛΕ_ΕΔΩ_ΤΟ_ΟΝΟΜΑ'
--   AND Status IN ('2', '3')
-- GROUP BY TreatmentDescription, Allergen, Status
-- ORDER BY QNT DESC;
