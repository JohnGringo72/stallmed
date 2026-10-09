-- ============================================================
-- Διαγνωστικά: δεσμευμένο stock σε "κλεισμένες" παραγγελίες prick
-- ΜΟΝΟ SELECT -- δεν αλλάζει τίποτα στη βάση.
-- ============================================================

-- 1) Η ίδια η παραγγελία και τα αδέρφια της (ο ίδιος βασικός κωδικός).
--    Το OrderStatus είναι το κρίσιμο: μόνο 'Fulfilled'/'Cancelled'
--    εξαιρούνται από το δεσμευμένο. Οτιδήποτε άλλο μετράει.
SELECT OrderID, OrderCode, OrderStatus,
       ShippedAt, PreparedAt, ShippingCarrier, CourierTrackingCode,
       DoctorName, Company, OrderDate, UpdatedAt
FROM DoctorOrders
WHERE OrderCode LIKE 'B26052079%'
ORDER BY OrderCode;


-- 2) Οι γραμμές της παραγγελίας: ζητήθηκαν / δεσμεύτηκαν / ακυρώθηκαν.
SELECT l.OrderLineID, o.OrderCode, o.OrderStatus,
       l.CodePrick, l.ProductTypeCode,
       l.QuantityRequested, l.QuantityAllocated, l.QuantityCancelled,
       l.LineStatus, l.UpdatedAt
FROM DoctorOrderLines l
JOIN DoctorOrders o ON o.OrderID = l.OrderID
WHERE o.OrderCode LIKE 'B26052079%'
ORDER BY o.OrderCode, l.CodePrick;


-- 3) Οι δεσμεύσεις της παραγγελίας πάνω σε συγκεκριμένες παραλαβές.
--    Active = κρατάει ακόμη εμπόρευμα. Reversed = έχει επιστραφεί.
SELECT a.AllocationID, o.OrderCode, o.OrderStatus,
       l.CodePrick, l.ProductTypeCode,
       a.QuantityAllocated, a.AllocationStatus, a.AllocationDate,
       a.ReceiptID, r.QuantityReceived, r.QuantityRemaining, r.IsDepleted
FROM OrderAllocations a
JOIN DoctorOrderLines l ON l.OrderLineID = a.OrderLineID
JOIN DoctorOrders o     ON o.OrderID = l.OrderID
LEFT JOIN StockReceipts r ON r.ReceiptID = a.ReceiptID
WHERE o.OrderCode LIKE 'B26052079%'
ORDER BY a.AllocationDate;


-- 4) Πλήρης εικόνα για τον C-002: ποιος κρατάει τι.
--    Άθροισε τα Active ανά OrderStatus και σύγκρινε με το ελεύθερο υπόλοιπο.
SELECT o.OrderStatus,
       COUNT(DISTINCT o.OrderID) AS Paraggelies,
       SUM(a.QuantityAllocated)  AS Desmeumena
FROM OrderAllocations a
JOIN DoctorOrderLines l ON l.OrderLineID = a.OrderLineID
JOIN DoctorOrders o     ON o.OrderID = l.OrderID
WHERE a.AllocationStatus = 'Active'
  AND l.CodePrick = 'C-002'
GROUP BY o.OrderStatus
ORDER BY Desmeumena DESC;

-- 4β) Το ελεύθερο (μη δεσμευμένο) υπόλοιπο του C-002 στις παραλαβές.
SELECT ProductTypeCode,
       SUM(QuantityReceived)  AS Paralifthikan,
       SUM(QuantityRemaining) AS Eleuthera,
       COUNT(*)               AS Paralaves
FROM StockReceipts
WHERE CodePrick = 'C-002'
  AND IsDepleted = 0
GROUP BY ProductTypeCode;


-- 5) ΤΟ ΣΗΜΑΝΤΙΚΟ: ενεργές δεσμεύσεις που ανήκουν σε ΚΛΕΙΣΤΕΣ παραγγελίες.
--    Αυτές έχουν ήδη αφαιρέσει εμπόρευμα από τις παραλαβές, αλλά η παραγγελία
--    τους δεν μετράει πουθενά -- άρα τα τεμάχια γίνονται αόρατα στο απόθεμα.
--    Το ShippingCarrier IS NULL δείχνει διορθωτικό κλείσιμο (δεν στάλθηκε ποτέ).
SELECT o.OrderCode, o.OrderStatus, o.ShippedAt, o.ShippingCarrier,
       l.CodePrick, l.ProductTypeCode,
       SUM(a.QuantityAllocated) AS EnergesDesmeuseis
FROM OrderAllocations a
JOIN DoctorOrderLines l ON l.OrderLineID = a.OrderLineID
JOIN DoctorOrders o     ON o.OrderID = l.OrderID
WHERE a.AllocationStatus = 'Active'
  AND o.OrderStatus IN ('Fulfilled', 'Cancelled')
GROUP BY o.OrderCode, o.OrderStatus, o.ShippedAt, o.ShippingCarrier,
         l.CodePrick, l.ProductTypeCode
HAVING EnergesDesmeuseis > 0
ORDER BY o.ShippingCarrier IS NULL DESC, EnergesDesmeuseis DESC
LIMIT 60;


-- 6) Η αντίστροφη όψη: παραγγελίες που ΔΕΝ είναι Fulfilled/Cancelled
--    και κρατάνε δεσμευμένο τον C-002. Αυτές εμφανίζονται στον φακό.
SELECT o.OrderCode, o.OrderStatus, o.PreparedAt, o.ShippedAt,
       l.ProductTypeCode, SUM(a.QuantityAllocated) AS Desmeumena
FROM OrderAllocations a
JOIN DoctorOrderLines l ON l.OrderLineID = a.OrderLineID
JOIN DoctorOrders o     ON o.OrderID = l.OrderID
WHERE a.AllocationStatus = 'Active'
  AND l.CodePrick = 'C-002'
  AND o.OrderStatus NOT IN ('Fulfilled', 'Cancelled')
GROUP BY o.OrderCode, o.OrderStatus, o.PreparedAt, o.ShippedAt, l.ProductTypeCode
ORDER BY Desmeumena DESC;
