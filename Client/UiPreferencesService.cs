using Blazored.LocalStorage;

namespace StallmedManager.Client
{
    public class UiPreferencesService
    {
        private readonly ILocalStorageService localStorage;
        private bool showHints = true;
        private bool initialized = false;

        public event EventHandler? StatusChanged;

        public UiPreferencesService(ILocalStorageService localStorage)
        {
            this.localStorage = localStorage;
        }

        public bool ShowHints => showHints;

        public async Task Initialize()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                var stored = await localStorage.GetItemAsync<bool?>("showHints");
                showHints = stored ?? true;
            }
            catch
            {
                showHints = true;
            }
        }

        public async Task SetShowHints(bool value)
        {
            showHints = value;
            await localStorage.SetItemAsync("showHints", value);
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
