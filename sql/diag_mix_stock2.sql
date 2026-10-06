-- ============================================================
-- Διαγνωστικά #2 για το στοκ στα Μείγματα -- ΜΟΝΟ SELECT
-- ============================================================

-- A) Ανά είδος ΚΑΙ ανά εταιρεία: παραγγελίες δίπλα στο στοκ.
--    Αν ένα είδος έχει παραγγελίες στο CompanyID '2' αλλά στοκ στο '1'
--    (ή το ανάποδο), τότε με φίλτρο εταιρείας το στοκ εξαφανίζεται.
SELECT TRIM(TreatmentDescription) AS Eidos,
       CompanyID,
       SUM(CASE WHEN REPLACE(Patient,' ','') IN ('AA','ΑΑ','AΑ','ΑA')
                THEN 0 ELSE QNT END)                                   AS Paraggelies,
       SUM(CASE WHEN REPLACE(Patient,' ','') IN ('AA','ΑΑ','AΑ','ΑA')
                 AND Status='3' THEN QNT ELSE 0 END)                   AS Edo,
       SUM(CASE WHEN REPLACE(Patient,' ','') IN ('AA','ΑΑ','AΑ','ΑA')
                 AND Status='2' THEN QNT ELSE 0 END)                   AS Anamenetai
FROM WebOrders
WHERE TreatmentDescription IS NOT NULL
  AND Status <> '5'
  AND (TreatmentDescription LIKE 'BELTA%' OR TreatmentDescription LIKE 'STALORAL%')
GROUP BY TRIM(TreatmentDescription), CompanyID
HAVING Edo > 0 OR Anamenetai > 0 OR Paraggelies > 0
ORDER BY Eidos, CompanyID;


-- B) Τα αλλεργιογόνα των γραμμών αποθήκης, ανά είδος.
--    Η σελίδα ταιριάζει το στοκ ανά ΜΕΙΓΜΑ (είδος + αλλεργιογόνο).
--    Αν εδώ το Allergen είναι κενό ή γράφεται αλλιώς απ' ό,τι στις
--    παραγγελίες των ασθενών, η γραμμή του μείγματος βγαίνει 0.
SELECT TRIM(TreatmentDescription) AS Eidos,
       CompanyID,
       CONCAT('[', IFNULL(Allergen,'<NULL>'), ']') AS Allergiogono,
       Status,
       COUNT(*) AS Rows_,
       SUM(QNT) AS QNT
FROM WebOrders
WHERE REPLACE(Patient,' ','') IN ('AA','ΑΑ','AΑ','ΑA')
  AND Status IN ('2','3')
GROUP BY TRIM(TreatmentDescription), CompanyID, Allergen, Status
ORDER BY QNT DESC
LIMIT 60;


-- C) Για ένα συγκεκριμένο είδος: πώς γράφεται το αλλεργιογόνο
--    στις παραγγελίες ασθενών, για σύγκριση με το (B).
--    Άλλαξε το όνομα του είδους σε αυτό που σε ενδιαφέρει.
SELECT CONCAT('[', IFNULL(Allergen,'<NULL>'), ']') AS Allergiogono,
       COUNT(*) AS Rows_,
       SUM(QNT) AS QNT
FROM WebOrders
WHERE TRIM(TreatmentDescription) = 'Staloral 300 (MT)'
  AND REPLACE(Patient,' ','') NOT IN ('AA','ΑΑ','AΑ','ΑA')
  AND Status <> '5'
GROUP BY Allergen
ORDER BY QNT DESC
LIMIT 30;
