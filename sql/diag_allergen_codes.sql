-- ============================================================
-- Από πού θα πάρουν κωδικό τα αλλεργιογόνα των Staloral
-- ΜΟΝΟ SELECT -- δεν αλλάζει τίποτα στη βάση.
-- ============================================================
-- Τα Belta γράφουν τον κωδικό μέσα στο κείμενο ("P-012 Olive 50"),
-- οπότε βγαίνει μόνος του. Τα Staloral γράφουν μόνο όνομα
-- ("Wall Pellitory"), άρα ο κωδικός πρέπει να βρεθεί αλλού.

-- 1) Ο κατάλογος κωδικών που ήδη έχει η εφαρμογή (πίνακας AllergenCodes).
SELECT CodePrick, Company, Description, DescriptionGreek, Category
FROM AllergenCodes
WHERE IsActive = 1
ORDER BY CodePrick;


-- 2) Τα αλλεργιογόνα που εμφανίζονται στα Staloral, όπως γράφονται.
SELECT TRIM(Allergen) AS Allergiogono,
       COUNT(*)       AS Grammes,
       SUM(QNT)       AS QNT
FROM WebOrders
WHERE UPPER(TreatmentDescription) LIKE 'STALORAL%'
  AND Allergen IS NOT NULL AND TRIM(Allergen) <> ''
GROUP BY TRIM(Allergen)
ORDER BY QNT DESC;


-- 3) ΔΟΚΙΜΗ: πόσα από αυτά ταιριάζουν μόνα τους με τον κατάλογο.
--    Όπου το CodePrick είναι NULL, δεν βρέθηκε αντιστοιχία με το όνομα.
SELECT s.Allergiogono,
       a.CodePrick,
       a.Description AS OnomaStonKatalogo
FROM (
    SELECT DISTINCT TRIM(Allergen) AS Allergiogono
    FROM WebOrders
    WHERE UPPER(TreatmentDescription) LIKE 'STALORAL%'
      AND Allergen IS NOT NULL AND TRIM(Allergen) <> ''
) s
LEFT JOIN AllergenCodes a
       ON a.IsActive = 1
      AND (s.Allergiogono LIKE CONCAT('%', a.Description, '%')
           OR a.Description LIKE CONCAT('%', s.Allergiogono, '%'))
ORDER BY s.Allergiogono, a.CodePrick;
