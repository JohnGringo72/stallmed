-- ============================================================
-- Από πού προκύπτει το «Αλλού 28» για τον M-001 (τύπος 92)
-- ΜΟΝΟ SELECT
-- ============================================================

-- 1) Ποιες παραγγελίες κρατάνε τα 28, μία γραμμή ανά παραγγελία.
--    Κοίτα το OrderStatus και το ShippedAt: ό,τι δεν έχει σταλεί
--    ΠΡΑΓΜΑΤΙΚΑ κρατάει εμπόρευμα που κάθεται στο ράφι.
SELECT o.OrderCode,
       o.OrderStatus,
       COALESCE(d.FullName, o.DoctorName) AS Giatros,
       o.Company,
       o.OrderDate,
       o.PreparedAt,
       o.ShippedAt,
       o.ShippingCarrier,
       SUM(a.QuantityAllocated) AS Desmeumena
FROM OrderAllocations a
JOIN DoctorOrderLines l ON l.OrderLineID = a.OrderLineID
JOIN DoctorOrders o     ON o.OrderID = l.OrderID
LEFT JOIN Doctors d     ON d.DoctorID = o.DoctorID
WHERE a.AllocationStatus = 'Active'
  AND l.CodePrick = 'M-001'
  AND l.ProductTypeCode = '92'
  AND o.OrderStatus NOT IN ('Fulfilled', 'Cancelled')
GROUP BY o.OrderCode, o.OrderStatus, Giatros, o.Company, o.OrderDate,
         o.PreparedAt, o.ShippedAt, o.ShippingCarrier
ORDER BY o.OrderDate;


-- 2) Το ελεύθερο υπόλοιπο στις παραλαβές -- το «Διαθέσιμο 5».
SELECT SUM(QuantityRemaining) AS Eleuthera,
       COUNT(*)               AS Paralaves
FROM StockReceipts
WHERE CodePrick = 'M-001'
  AND ProductTypeCode = '92'
  AND IsDepleted = 0;


-- 3) Ηλικία των δεσμεύσεων: παλιές ανοιχτές παραγγελίες είναι ο συνήθης
--    λόγος που «λείπει» εμπόρευμα -- κρατιέται για κάποιον εδώ και μήνες.
SELECT o.OrderCode,
       o.OrderStatus,
       a.AllocationDate,
       DATEDIFF(CURDATE(), a.AllocationDate) AS MeresDesmeumeno,
       a.QuantityAllocated
FROM OrderAllocations a
JOIN DoctorOrderLines l ON l.OrderLineID = a.OrderLineID
JOIN DoctorOrders o     ON o.OrderID = l.OrderID
WHERE a.AllocationStatus = 'Active'
  AND l.CodePrick = 'M-001'
  AND l.ProductTypeCode = '92'
  AND o.OrderStatus NOT IN ('Fulfilled', 'Cancelled')
ORDER BY a.AllocationDate;


-- 4) Η συνολική εικόνα: 5 ελεύθερα + 28 δεσμευμένα = πόσα υπάρχουν όντως;
SELECT
  (SELECT COALESCE(SUM(QuantityRemaining), 0) FROM StockReceipts
    WHERE CodePrick = 'M-001' AND ProductTypeCode = '92' AND IsDepleted = 0) AS Eleuthera,
  (SELECT COALESCE(SUM(a.QuantityAllocated), 0)
     FROM OrderAllocations a
     JOIN DoctorOrderLines l ON l.OrderLineID = a.OrderLineID
     JOIN DoctorOrders o     ON o.OrderID = l.OrderID
    WHERE a.AllocationStatus = 'Active'
      AND l.CodePrick = 'M-001' AND l.ProductTypeCode = '92'
      AND o.OrderStatus NOT IN ('Fulfilled', 'Cancelled'))                    AS Desmeumena;
