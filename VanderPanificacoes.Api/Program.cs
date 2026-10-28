using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using VanderPanificacoes.Api.Data;
using VanderPanificacoes.Api.Models;
using VanderPanificacoes.Api.Services;
using VanderPanificacoes.Api.Commands;

var builder = WebApplication.CreateBuilder(args);

byte[]? chaveConfigurada = null;
try
{
    var valorChave = builder.Configuration["JwtDesenvolvimento:ChaveBase64"];
    if (!string.IsNullOrWhiteSpace(valorChave)) chaveConfigurada = Convert.FromBase64String(valorChave);
}
catch (FormatException) { }
var configuracaoToken = new ConfiguracaoToken(builder.Environment.IsDevelopment() && chaveConfigurada is { Length: >= 32 },
    chaveConfigurada is { Length: >= 32 } && builder.Environment.IsDevelopment() ? chaveConfigurada : RandomNumberGenerator.GetBytes(32));
builder.Services.AddSingleton(configuracaoToken);
builder.Services.AddScoped<TokenDesenvolvimentoService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = "VanderPanificacoes.Dev",
        ValidateAudience = true, ValidAudience = "VanderPanificacoes.Api",
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(configuracaoToken.Chave),
        ValidateLifetime = true, RequireExpirationTime = true, ClockSkew = TimeSpan.Zero,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        NameClaimType = JwtRegisteredClaimNames.Sub, RoleClaimType = "role"
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (!configuracaoToken.Configurada ||
                !int.TryParse(context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id))
            {
                context.Fail("Token inválido.");
                return;
            }
            var perfil = context.Principal?.FindFirst("role")?.Value;
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            if (!await db.Usuarios.AsNoTracking().AnyAsync(u => u.Id == id && u.Ativo && u.Perfil == perfil, context.HttpContext.RequestAborted))
                context.Fail("Conta inativa ou inválida.");
        }
    };
});
builder.Services.AddAuthorization();

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddScoped<SenhaService>();
builder.Services.AddScoped<IAutenticacaoService, AutenticacaoService>();
builder.Services.AddScoped<IHistoricoEstoqueService, HistoricoEstoqueService>();
builder.Services.AddScoped<IVendedoresService, VendedoresService>();
builder.Services.AddScoped<IClientesService, ClientesService>();
builder.Services.AddScoped<ICarteiraService, CarteiraService>();
builder.Services.AddScoped<ICondicoesPagamentoService, CondicoesPagamentoService>();
builder.Services.AddScoped<ILotesService, LotesService>();
builder.Services.AddScoped<IDetalhesLoteService, DetalhesLoteService>();
builder.Services.AddScoped<ISugestaoLoteService, SugestaoLoteService>();
builder.Services.AddScoped<CalculoVendaService>();
builder.Services.AddScoped<IConferenciaLoteService, ConferenciaLoteService>();
builder.Services.AddScoped<IConfirmacaoLoteService, ConfirmacaoLoteService>();
builder.Services.AddScoped<ICancelamentoLoteService, CancelamentoLoteService>();
builder.Services.AddScoped<IParcelasService, ParcelasService>();
builder.Services.AddScoped<IRecebimentoParcelaService, RecebimentoParcelaService>();
builder.Services.AddScoped<ValidacaoLoteService>();
builder.Services.AddScoped<IRecebimentoLoteService, RecebimentoLoteService>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
var connectionString = builder.Configuration.GetConnectionString("MySql");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0))));

var app = builder.Build();

if (args.Length == 1 && args[0] == "criar-admin")
{
    Environment.ExitCode = await CriarAdmin.ExecutarAsync(app.Services);
    return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapGet("/", () => Results.Redirect("http://localhost:5300"));
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
