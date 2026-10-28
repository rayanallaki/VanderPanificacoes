using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;

namespace VanderPanificacoes.Web.Services;

public sealed class ApiClient(HttpClient http, IHttpContextAccessor contexto)
{
    public async Task<JsonObject> Objeto(string rota, CancellationToken ct) =>
        (await Enviar(HttpMethod.Get, rota, null, ct))?.AsObject() ?? new JsonObject();

    public async Task<List<JsonObject>> Lista(string rota, CancellationToken ct) =>
        (await Enviar(HttpMethod.Get, rota, null, ct))?.AsArray().OfType<JsonObject>().ToList() ?? [];

    public async Task<JsonNode?> Enviar(HttpMethod metodo, string rota, object? dados, CancellationToken ct, bool login = false)
    {
        if (!rota.StartsWith("api/", StringComparison.Ordinal)) throw new ArgumentException("Rota inválida.");
        using var request = new HttpRequestMessage(metodo, rota);
        if (!login)
        {
            var token = await contexto.HttpContext!.GetTokenAsync("access_token");
            if (string.IsNullOrEmpty(token)) throw new FalhaApi(HttpStatusCode.Unauthorized, "Entre novamente para continuar.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (dados is not null) request.Content = JsonContent.Create(dados);
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, ct); }
        catch (HttpRequestException) { throw new FalhaApi(HttpStatusCode.ServiceUnavailable, "Não foi possível conectar à API. Verifique se ela está em execução em http://localhost:5296."); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new FalhaApi(HttpStatusCode.GatewayTimeout, "A API demorou para responder. Confira o resultado antes de iniciar uma nova operação."); }
        using (response)
        {
            JsonNode? corpo = null;
            if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
                corpo = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
            if (!response.IsSuccessStatusCode)
            {
                var mensagem = corpo?["mensagem"]?.ToString();
                if (corpo?["erros"] is JsonArray erros) mensagem = string.Join(" ", erros.Select(e => e?.ToString()));
                if (corpo?["errors"] is JsonObject campos) mensagem = string.Join(" ", campos.SelectMany(c => c.Value is JsonArray a ? a.Select(v => v?.ToString()) : [c.Value?.ToString()]));
                throw new FalhaApi(response.StatusCode, mensagem ?? (response.StatusCode == HttpStatusCode.Unauthorized ? "Login inválido ou sessão expirada." : $"Não foi possível concluir a operação (HTTP {(int)response.StatusCode})."));
            }
            return corpo;
        }
    }
}

public sealed class FalhaApi(HttpStatusCode status, string mensagem) : Exception(mensagem)
{
    public HttpStatusCode Status { get; } = status;
}
