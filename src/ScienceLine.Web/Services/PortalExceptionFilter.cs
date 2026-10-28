using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ScienceLine.Web.Models;

namespace ScienceLine.Web.Services;
public class PortalExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext c)
    {
        if(c.Exception is not PortalRuleException ex)return;
        c.Result=new ViewResult{ViewName="~/Views/Shared/RuleError.cshtml",StatusCode=ex.Status,
            ViewData=new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<RuleError>(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),c.ModelState){Model=new(ex.Status,ex.Message)}};
        c.ExceptionHandled=true;
    }
}
