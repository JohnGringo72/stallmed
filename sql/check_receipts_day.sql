-- ============================================================
-- Καταχωρήθηκε η παραλαβή prick της 8/10/2026;
-- ΜΟΝΟ SELECT -- δεν αλλάζει τίποτα στη βάση.
-- ============================================================

-- 1) Ό,τι καταχωρήθηκε ΜΕ ημερομηνία παραλαβής 08/10/2026.
SELECT r.ReceiptID,
       r.CodePrick,
       a.Description        AS Allergiogono,
       r.ProductTypeCode,
       r.QuantityReceived   AS Irthan,
       r.QuantityRemaining  AS Eleuthera,
       r.ReceivedDate       AS ImerominiaParalavis,
       r.CreatedAt          AS PoteKataxorithike,
       CONCAT(u.Firstname, ' ', u.Lastname) AS PoiosToKataxorise,
       r.Notes
FROM StockReceipts r
LEFT JOIN AllergenCodes a ON a.CodePrick = r.CodePrick
LEFT JOIN Users u         ON u.IdUser = r.CreatedBy
WHERE r.ReceivedDate = '2026-10-08'
ORDER BY r.CreatedAt, r.CodePrick;


-- 2) Ό,τι ΠΛΗΚΤΡΟΛΟΓΗΘΗΚΕ στις 8/10, ανεξάρτητα από την ημερομηνία
--    παραλαβής που έγραψε ο χρήστης (μπορεί να έβαλε λάθος ημερομηνία).
SELECT r.ReceiptID, r.CodePrick, r.ProductTypeCode,
       r.QuantityReceived, r.ReceivedDate, r.CreatedAt,
       CONCAT(u.Firstname, ' ', u.Lastname) AS PoiosToKataxorise
FROM StockReceipts r
LEFT JOIN Users u ON u.IdUser = r.CreatedBy
WHERE DATE(r.CreatedAt) = '2026-10-08'
ORDER BY r.CreatedAt;


-- 3) Σύνοψη των τελευταίων 10 ημερών: πόσες παραλαβές ανά ημέρα.
--    Αν η 8/10 λείπει τελείως από τη λίστα, δεν καταχωρήθηκε ποτέ.
SELECT r.ReceivedDate          AS Imerominia,
       COUNT(*)                AS Eggrafes,
       SUM(r.QuantityReceived) AS Temachia,
       MIN(r.CreatedAt)        AS ProtiKataxorisi,
       MAX(r.CreatedAt)        AS TeleftaiaKataxorisi
FROM StockReceipts r
WHERE r.ReceivedDate >= '2026-09-28'
GROUP BY r.ReceivedDate
ORDER BY r.ReceivedDate DESC;


-- 4) Αν βρεθούν διπλές καταχωρήσεις (μπήκε δύο φορές από αβεβαιότητα):
--    ίδιος κωδικός + τύπος + ημερομηνία, περισσότερες από μία εγγραφές.
SELECT r.CodePrick, r.ProductTypeCode, r.ReceivedDate,
       COUNT(*)                AS Fores,
       SUM(r.QuantityReceived) AS SynolikaTemachia,
       GROUP_CONCAT(r.ReceiptID ORDER BY r.ReceiptID) AS ReceiptIDs
FROM StockReceipts r
WHERE r.ReceivedDate >= '2026-09-28'
GROUP BY r.CodePrick, r.ProductTypeCode, r.ReceivedDate
HAVING Fores > 1
ORDER BY r.ReceivedDate DESC, Fores DESC;
