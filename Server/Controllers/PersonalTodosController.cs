using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    // ---- Προσωπική λίστα Todo (sql/personal_todos.sql) ----
    // Αυστηρά προσωπική: κάθε χρήστης βλέπει/αλλάζει μόνο τα δικά του -- και ο admin.
    // (Τα κοινά πράγματα, π.χ. γιορτές, μπαίνουν στο Ημερολόγιο: CalendarController.)
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PersonalTodosController : ControllerBase
    {
        private readonly StallmedContext _context;

        public PersonalTodosController(StallmedContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<List<PersonalTodoViewDto>>> GetTodos([FromQuery] int userId, [FromQuery] bool includeDone = false)
        {
            var query = _context.PersonalTodos.Where(t => t.UserID == userId);
            if (!includeDone)
                query = query.Where(t => !t.IsDone);

            var list = await query
                .OrderBy(t => t.IsDone)
                .ThenBy(t => t.DueDate == null)      // πρώτα όσα έχουν προθεσμία
                .ThenBy(t => t.DueDate)
                .ThenByDescending(t => t.TodoID)
                .ToListAsync();

            return Ok(list.Select(t => new PersonalTodoViewDto
            {
                TodoID = t.TodoID,
                UserID = t.UserID,
                TodoText = t.TodoText,
                Notes = t.Notes,
                Priority = t.Priority,
                DueDate = t.DueDate,
                IsDone = t.IsDone,
                DoneAt = t.DoneAt,
                CreatedAt = t.CreatedAt
            }).ToList());
        }

        [HttpPost("save")]
        public async Task<ActionResult<PersonalTodo>> Save([FromBody] SavePersonalTodoRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.TodoText))
                return BadRequest("Κενό κείμενο.");

            PersonalTodo todo;
            if (req.TodoID.HasValue)
            {
                todo = await _context.PersonalTodos.FindAsync(req.TodoID.Value);
                if (todo == null) return NotFound();
                if (todo.UserID != req.UserID) return NotFound();
            }
            else
            {
                // Νέα εγγραφή: πάντα στον χρήστη που την ανοίγει.
                todo = new PersonalTodo { UserID = req.UserID, CreatedAt = DateTime.Now };
                _context.PersonalTodos.Add(todo);
            }

            todo.TodoText = req.TodoText.Trim();
            todo.Notes = req.Notes;
            todo.Priority = string.IsNullOrEmpty(req.Priority) ? "Normal" : req.Priority;
            todo.DueDate = req.DueDate;
            todo.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(todo);
        }

        [HttpPost("toggle/{todoId:long}")]
        public async Task<ActionResult> ToggleDone(long todoId, [FromQuery] int userId)
        {
            var todo = await _context.PersonalTodos.FindAsync(todoId);
            if (todo == null) return NotFound();
            if (todo.UserID != userId) return NotFound();
            todo.IsDone = !todo.IsDone;
            todo.DoneAt = todo.IsDone ? DateTime.Now : null;
            todo.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { Success = true, todo.IsDone });
        }

        [HttpPost("delete/{todoId:long}")]
        public async Task<ActionResult> Delete(long todoId, [FromQuery] int userId)
        {
            var todo = await _context.PersonalTodos.FindAsync(todoId);
            if (todo == null) return NotFound();
            if (todo.UserID != userId) return NotFound();
            _context.PersonalTodos.Remove(todo);
            await _context.SaveChangesAsync();
            return Ok(new { Success = true });
        }
    }
}
