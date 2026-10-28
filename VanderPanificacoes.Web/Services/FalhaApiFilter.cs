using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace VanderPanificacoes.Web.Services;

public sealed class FalhaApiFilter : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not FalhaApi falha) return;
        context.ExceptionHandled = true;
        if (falha.Status == HttpStatusCode.Unauthorized)
        {
            await context.HttpContext.SignOutAsync();
            context.Result = new RedirectToActionResult("Entrar", "Conta", new { expirou = true });
            return;
        }
        context.Result = new ViewResult { ViewName = "~/Views/Shared/Falha.cshtml", StatusCode = (int)falha.Status,
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState) { Model = falha.Message } };
    }
}
