using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    public class PersonalTodosService
    {
        private readonly DataService dataService;

        public PersonalTodosService(DataService dataService)
        {
            this.dataService = dataService;
        }

        public async Task<List<PersonalTodoViewDto>> GetTodos(int userId, bool includeDone)
            => await dataService.Get<List<PersonalTodoViewDto>>($"api/personaltodos?userId={userId}&includeDone={includeDone}") ?? new();

        public async Task<PersonalTodo?> Save(SavePersonalTodoRequest req)
            => await dataService.Post<SavePersonalTodoRequest, PersonalTodo>("api/personaltodos/save", req);

        public async Task ToggleDone(long todoId, int userId)
            => await dataService.Post<object, object>($"api/personaltodos/toggle/{todoId}?userId={userId}", new { });

        public async Task Delete(long todoId, int userId)
            => await dataService.Post<object, object>($"api/personaltodos/delete/{todoId}?userId={userId}", new { });
    }
}
