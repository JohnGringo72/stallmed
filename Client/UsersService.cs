using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    public class UsersService
    {
        private readonly DataService dataService;

        public UsersService(DataService dataService)
        {
            this.dataService = dataService;
        }

        public async Task<List<UserAdminDto>> GetUsers()
            => await dataService.Get<List<UserAdminDto>>("api/users") ?? new();

        public async Task<List<string>> GetRoles()
            => await dataService.Get<List<string>>("api/users/roles") ?? new();

        public async Task<UserSaveResult> Save(SaveUserRequest req)
            => await dataService.Post<SaveUserRequest, UserSaveResult>("api/users/save", req);

        public async Task<UserSaveResult> SetPassword(SetUserPasswordRequest req)
            => await dataService.Post<SetUserPasswordRequest, UserSaveResult>("api/users/set-password", req);

        public async Task<UserSaveResult> SetActive(SetUserActiveRequest req)
            => await dataService.Post<SetUserActiveRequest, UserSaveResult>("api/users/set-active", req);
    }
}
