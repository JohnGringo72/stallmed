using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StallmedManager.Server.Services;
using StallmedManager.Services;

var builder = WebApplication.CreateBuilder(args);

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
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddDbContext<StallmedManager.Server.Models.StallmedContext>(x => x.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 34))));
builder.Services.AddSingleton<AesService>();
builder.Services.AddScoped<StockSearchService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddScoped<AiContextService>();
builder.Services.AddScoped<ChatBotService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();