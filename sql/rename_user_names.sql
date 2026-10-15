-- =====================================================================
-- Μετονομασία χρήστη: ενημερώνει τα ΑΝΤΙΓΡΑΦΑ του ονόματος που κρατούν
-- τα Tasks και το Ημερολόγιο από τη στιγμή της καταχώρησης.
-- (Παραγγελίες, παραλαβές, προσφορές κλπ. διαβάζουν το όνομα από τον
--  πίνακα Users, άρα αλλάζουν μόνα τους.)
--
-- ΒΗΜΑΤΑ:
--   1. Άλλαξε πρώτα το επώνυμο στη σελίδα Χρήστες (ή στον πίνακα Users).
--   2. Τρέξε το (Α) και βρες το IdUser.
--   3. Βάλε το IdUser στο SET @id και τρέξε το (Β).
-- Η αντιστοίχιση γίνεται με τον ΚΩΔΙΚΟ χρήστη, όχι με το κείμενο,
-- οπότε δεν επηρεάζουν τόνοι/κεφαλαία ή ορθογραφικά λάθη.
-- =====================================================================

-- (Α) Βρες τον χρήστη
SELECT IdUser, Firstname, Lastname, Username
FROM Users
WHERE Firstname LIKE '%Γιωτα%' OR Firstname LIKE '%ΓΙΩΤΑ%';


-- (Β) Ενημέρωση των αντιγράφων -- βάλε το σωστό IdUser
SET @id = 0;
SET @name = (SELECT TRIM(CONCAT(TRIM(Firstname), ' ', TRIM(Lastname))) FROM Users WHERE IdUser = @id);

-- Έλεγχος: πρέπει να δείχνει το ΝΕΟ όνομα. Αν είναι NULL, το @id είναι λάθος -- σταμάτα.
SELECT @id AS IdUser, @name AS NeoOnoma;

UPDATE IssueTasks       SET CreatedByName  = @name WHERE CreatedBy       = @id AND @name IS NOT NULL;
UPDATE IssueTasks       SET AssignedName   = @name WHERE AssignedUserID  = @id AND AssignedToAll = 0 AND AssignedTeam IS NULL AND @name IS NOT NULL;
UPDATE IssueTasks       SET RegardingName  = @name WHERE RegardingUserID = @id AND @name IS NOT NULL;
UPDATE IssueComments    SET UserName       = @name WHERE UserID          = @id AND @name IS NOT NULL;
UPDATE IssueAttachments SET UploadedByName = @name WHERE UploadedBy      = @id AND @name IS NOT NULL;
UPDATE CalendarEvents   SET CreatedByName  = @name WHERE CreatedBy       = @id AND @name IS NOT NULL;
