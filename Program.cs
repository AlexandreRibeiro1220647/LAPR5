using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using TodoApi.Models;
using TodoApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<UserContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Add HttpContextAccessor to access the current user's claim
builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient<AuthPatientService>();


string domain = builder.Configuration["Auth0:Domain"];
string audience = builder.Configuration["Auth0:Audience"];
string clientId = builder.Configuration["Auth0:ClientId"];
string clientSecret = builder.Configuration["Auth0:ClientSecret"];



builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = $"https://{domain}";
    options.Audience = audience;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = $"https://{domain}",

        ValidateAudience = true,
        ValidAudience = audience,

        ValidateLifetime = true,

        // Automatically retrieve the signing keys from Auth0
        IssuerSigningKeyResolver = (token, securityToken, identifier, parameters) =>
        {
            var client = new HttpClient();
            var jwks = client.GetStringAsync($"https://{domain}/.well-known/jwks.json").Result;
            return new JsonWebKeySet(jwks).GetSigningKeys();
        }
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine("Authentication failed: " + context.Exception.Message);
            return Task.CompletedTask;
        }
    };
});
// Bind Auth0 settings from appsettings.json
builder.Services.Configure<Auth0Settings>(builder.Configuration.GetSection("Auth0"));


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PatientOnly", policy =>
        {
            policy.RequireClaim(Auth0Data.ROLES_URL, "Patient");
        });
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireClaim(Auth0Data.ROLES_URL, "Admin");
    });
    options.AddPolicy("DoctorOnly", policy =>
    {
        policy.RequireClaim(Auth0Data.ROLES_URL, "Doctor");
    });
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTransient<UserRegistrationService>();
builder.Services.AddTransient<AuthPatientService>();
builder.Services.AddTransient<RandomPasswordService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
