using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);


//builder.Services.AddTransient<IUserDatabase, UserDatabase>();  // NOTE: LOCAL AUTHENTICATION ADDED HERE; AddTransient() IS OK TO USE BECAUSE STATE IS SAVED TO THE DRIVE

// NOTE: the following block of code is newly added
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
        IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("THIS IS THE SECRET KEY")) // NOTE: THIS SHOULD BE A SECRET KEY NOT TO BE SHARED; A GUID IS RECOMMENDED
    };
});
// NOTE: end block

// Add services to the container.
var connectionString = File.ReadAllText("connectionString"); 

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddDbContext<StallmedManager.Server.Models.StallmedContext>(x => x.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 34))));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    // Θα το χρειαστώ (;) όταν βάλω HTTPS
    //app.UseHsts();
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
