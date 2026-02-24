using Application.Interfaces;
using Application.Services;
using Infrastructure.Context;
using Infrastructure.Helpers;
using Infrastructure.Repository;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Bind to Render dynamic port
builder.WebHost.ConfigureKestrel(options =>
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    options.ListenAnyIP(int.Parse(port));
});

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Scoped Services
builder.Services.AddScoped<IUserRequestContext, UserRequestContext>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IDynamicDbContextFactory, DynamicDbContextFactory>();
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserKeyService, UserKeyService>();
builder.Services.AddScoped<ITokenBlacklistService, TokenBlacklistService>();
builder.Services.AddScoped<ItemService>();
builder.Services.AddScoped<CustomerAccountService>();
builder.Services.AddScoped<SalesAccountService>();
builder.Services.AddScoped<PaymentTermService>();
builder.Services.AddScoped<InvoiceDetailsService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<CommonLookupService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<SalesReturnService>();
builder.Services.AddScoped<IValidationService, ValidationService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<GRNService>();
builder.Services.AddScoped<GREService>();
builder.Services.AddScoped<PurchaseOrderService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<LookupService>();
builder.Services.AddScoped<CodeService>();
builder.Services.AddScoped<BankService>();
builder.Services.AddScoped<UserService>();

builder.Services.AddMemoryCache();

// Database
builder.Services.AddDbContext<MainDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MainDb")));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "";
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey))
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                if (context.Exception is SecurityTokenExpiredException)
                {
                    context.Response.Headers["Token-Expired"] = "true";
                }
                return Task.CompletedTask;
            }
        };
    });

var app = builder.Build();

// Middleware
app.UseAuthentication();
app.UseMiddleware<Api.Middlewares.JwtSessionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();