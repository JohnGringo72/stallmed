-- ============================================================
-- Πού βρίσκεται ο κωδικός θεραπείας (60, 62, 47, 42 ...)
-- ΜΟΝΟ SELECT -- δεν αλλάζει τίποτα στη βάση.
-- ============================================================

-- 1) Όλες οι στήλες του WebOrders -- μήπως ο κωδικός είναι ήδη εκεί
--    σε στήλη που δεν χρησιμοποιεί η εφαρμογή.
SHOW COLUMNS FROM WebOrders;


-- 2) Μία πραγματική γραμμή, να δούμε τι έχει κάθε στήλη.
SELECT * FROM WebOrders
WHERE TRIM(TreatmentDescription) = 'Staloral 300 (MT)'
LIMIT 3;


-- 3) Ποιοι πίνακες της βάσης έχουν στήλη με "treatment" ή "code" στο όνομα.
--    Εδώ θα φανεί ο πίνακας που κρατάει την αντιστοίχιση ονόματος -> κωδικού.
SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND (COLUMN_NAME LIKE '%treatment%' OR COLUMN_NAME LIKE '%code%')
ORDER BY TABLE_NAME, ORDINAL_POSITION;


-- 4) Όλοι οι πίνακες, αν το (3) δεν δείξει κάτι χρήσιμο.
SHOW TABLES;


-- 5) Το Ref του WebOrders ανά είδος -- αν το Ref είναι ο κωδικός,
--    κάθε είδος θα έχει ΕΝΑ μόνο Ref.
SELECT TRIM(TreatmentDescription) AS Eidos,
       COUNT(DISTINCT Ref)  AS DiaforetikaRef,
       MIN(Ref)             AS Paradeigma,
       COUNT(*)             AS Grammes
FROM WebOrders
WHERE TreatmentDescription IS NOT NULL
GROUP BY TRIM(TreatmentDescription)
ORDER BY Grammes DESC
LIMIT 30;


-- 6) Το ίδιο για τα αλλεργιογόνα των Staloral: υπάρχει κωδικός κάπου;
SELECT DISTINCT Allergen
FROM WebOrders
WHERE TRIM(TreatmentDescription) = 'Staloral 300 (MT)'
  AND Allergen IS NOT NULL
ORDER BY Allergen
LIMIT 30;
