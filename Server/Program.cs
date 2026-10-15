using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StallmedManager.Server.Services;
using StallmedManager.Services;

var builder = WebApplication.CreateBuilder(args);
// Κλειδιά ειδοποιήσεων push σε δικό τους αρχείο: ανεβαίνει χωρίς να πειραχτεί
// το appsettings.json του server. Αν λείπει, απλώς δεν στέλνονται push.
builder.Configuration.AddJsonFile("appsettings.Push.json", optional: true, reloadOnChange: true);
// Στοιχεία σύνδεσης με τον Traccar (Παρακολούθηση Συσκευών) -- μόνο στον server.
builder.Configuration.AddJsonFile("appsettings.Tracking.json", optional: true, reloadOnChange: true);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateAudience = true,
        ValidAudience = "domain.com",
        ValidateIssuer = true,
        ValidIssuer = "domain.com",
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("THIS IS THE SECRET KEY"))
    };
});

builder.Services.AddAuthorization(options =>
{
    // Deny-list αντί για allow-list: αποκλείει μόνο το warehouse, χωρίς να χρειάζεται να ξέρει
    // όλους τους υπόλοιπους ρόλους που υπάρχουν ήδη στο σύστημα.
    options.AddPolicy("NotWarehouse", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.Identity?.IsAuthenticated == true && !ctx.User.IsInRole("warehouse")));

    // Case-insensitive έλεγχος ρόλου (το IsInRole συγκρίνει case-sensitive
    // και οι τιμές Role στον πίνακα Users δεν έχουν εγγυημένη μορφή).
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.Identity?.IsAuthenticated == true &&
            ctx.User.Claims.Any(c => c.Type == System.Security.Claims.ClaimTypes.Role &&
                string.Equals(c.Value?.Trim(), "admin", StringComparison.OrdinalIgnoreCase))));

    // Sales module (Επισκέψεις/Εξοδολόγιο): μόνο πωλητές (vendor) και admin.
    options.AddPolicy("VendorOrAdmin", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.Identity?.IsAuthenticated == true &&
            ctx.User.Claims.Any(c => c.Type == System.Security.Claims.ClaimTypes.Role &&
                (string.Equals(c.Value?.Trim(), "vendor", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(c.Value?.Trim(), "admin", StringComparison.OrdinalIgnoreCase)))));
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddDbContext<StallmedManager.Server.Models.StallmedContext>(x => x.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 34))));
builder.Services.AddSingleton<AesService>();
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddScoped<StockSearchService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
// builder.Services.AddScoped<AiContextService>(); // ΑΝΕΝΕΡΓΟ (23/07/2026): χρησιμοποιούνταν μόνο από τον σχολιασμένο AiChatController
builder.Services.AddScoped<ChatBotService>();
builder.Services.AddScoped<QuotePdfService>();
builder.Services.AddScoped<QuoteEmailService>();
// Singleton: στέλνει στο παρασκήνιο με δικό του scope (DbContext), μετά το τέλος του request.
builder.Services.AddSingleton<PushNotificationService>();
// Client του υπάρχοντος Traccar (μόνο ανάγνωση μέσω REST API).
builder.Services.AddHttpClient<TraccarClient>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
}

// Η σελίδα και ο κατάλογος αρχείων της εφαρμογής δεν πρέπει να μένουν στην
// cache του browser: αλλιώς μετά από ανέβασμα νέας έκδοσης ο χρήστης συνεχίζει
// να τρέχει την παλιά και χρειάζεται χειροκίνητο καθάρισμα.
// Τα υπόλοιπα αρχεία (.dll, .wasm) κρατούν την cache τους κανονικά -- ο Blazor
// τα ελέγχει με τα hash που διαβάζει από το blazor.boot.json.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    var noCache =
        path.EndsWith("blazor.boot.json", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith("index.html", StringComparison.OrdinalIgnoreCase) ||
        // Ο service worker των ειδοποιήσεων και το manifest πρέπει να ανανεώνονται με κάθε έκδοση.
        path.EndsWith("service-worker.js", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase) ||
        path == "/" || path == "";

    if (noCache)
    {
        // OnStarting: μπαίνει τελευταία στιγμή, ώστε να μην το σβήσει
        // το middleware των στατικών αρχείων.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";
            return Task.CompletedTask;
        });
    }

    await next();
});

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();