using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StallmedManager.Shared.Models
{
    // ---- Προσωπική λίστα Todo ανά χρήστη (sql/personal_todos.sql) ----
    // Ιδιωτική: κάθε χρήστης βλέπει και διαχειρίζεται μόνο τις δικές του εγγραφές.

    [Table("PersonalTodos")]
    public class PersonalTodo
    {
        [Key]
        public long TodoID { get; set; }
        public int UserID { get; set; }
        public string TodoText { get; set; } = "";
        public string? Notes { get; set; }
        public string Priority { get; set; } = "Normal";
        public DateTime? DueDate { get; set; }
        public bool IsDone { get; set; }
        public DateTime? DoneAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PersonalTodoViewDto
    {
        public long TodoID { get; set; }
        public int UserID { get; set; }
        public string? OwnerName { get; set; }   // συμπληρώνεται μόνο για admin (βλέπει όλων)
        public string TodoText { get; set; } = "";
        public string? Notes { get; set; }
        public string Priority { get; set; } = "Normal";
        public DateTime? DueDate { get; set; }
        public bool IsDone { get; set; }
        public DateTime? DoneAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SavePersonalTodoRequest
    {
        public long? TodoID { get; set; }    // null = νέο
        public int UserID { get; set; }
        public string TodoText { get; set; } = "";
        public string? Notes { get; set; }
        public string Priority { get; set; } = "Normal";
        public DateTime? DueDate { get; set; }
    }
}
