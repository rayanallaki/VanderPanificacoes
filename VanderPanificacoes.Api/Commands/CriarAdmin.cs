using System.Net.Mail;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Data;
using VanderPanificacoes.Api.Models;
using VanderPanificacoes.Api.Services;

namespace VanderPanificacoes.Api.Commands;

public static class CriarAdmin
{
    public static async Task<int> ExecutarAsync(IServiceProvider services)
    {
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Execute o comando em um terminal interativo.");
            return 1;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            if (await db.Usuarios.AnyAsync(u => u.Perfil == "Administrador"))
            {
                Console.Error.WriteLine("Já existe um administrador. Nenhuma conta foi criada.");
                return 1;
            }

            Console.Write("Nome: ");
            var nome = Console.ReadLine()?.Trim();
            Console.Write("E-mail: ");
            var email = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(nome) || nome.Length > 100 ||
                string.IsNullOrWhiteSpace(email) || email.Length > 150 ||
                !MailAddress.TryCreate(email, out var endereco) || endereco.Address != email)
            {
                Console.Error.WriteLine("Informe um nome e um e-mail válidos (máximo de 100 e 150 caracteres).");
                return 1;
            }

            if (await db.Usuarios.AnyAsync(u => u.Email == email))
            {
                Console.Error.WriteLine("Este e-mail já está cadastrado.");
                return 1;
            }

            var senha = LerSenha("Senha (mínimo de 8 caracteres): ");
            var confirmacao = LerSenha("Confirme a senha: ");
            if (string.IsNullOrWhiteSpace(senha) || senha.Length < 8 || senha != confirmacao)
            {
                Console.Error.WriteLine("A senha precisa ter pelo menos 8 caracteres e coincidir com a confirmação.");
                return 1;
            }

            var usuario = new Usuario { Nome = nome, Email = email, Perfil = "Administrador", Ativo = true };
            scope.ServiceProvider.GetRequiredService<SenhaService>().DefinirSenha(usuario, senha);
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync();
            Console.WriteLine("Administrador criado com sucesso.");
            return 0;
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or MySqlConnector.MySqlException)
        {
            Console.Error.WriteLine("Não foi possível criar o administrador. Verifique a conexão, as permissões e se a migração foi aplicada.");
            return 1;
        }
    }

    private static string LerSenha(string mensagem)
    {
        Console.Write(mensagem);
        var senha = new StringBuilder();
        while (true)
        {
            var tecla = Console.ReadKey(intercept: true);
            if (tecla.Key == ConsoleKey.Enter)
                break;
            if (tecla.Key == ConsoleKey.Backspace && senha.Length > 0)
                senha.Length--;
            else if (!char.IsControl(tecla.KeyChar))
                senha.Append(tecla.KeyChar);
        }
        Console.WriteLine();
        return senha.ToString();
    }
}
