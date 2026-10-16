using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Library.Web.Import;

public sealed class ImportExceptionFilter(ITempDataDictionaryFactory tempData) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (HttpMethods.IsPost(context.HttpContext.Request.Method))
        {
            tempData.GetTempData(context.HttpContext)["ImportMessage"] = "The import could not be completed. Please try again later.";
            context.Result = new RedirectToActionResult("Index", "Import", null);
        }
        else
            context.Result = new ViewResult { ViewName = "~/Views/Import/Unavailable.cshtml", StatusCode = 503 };
        context.ExceptionHandled = true;
    }
}
