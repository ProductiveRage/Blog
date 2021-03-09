using System.IO;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Blog.Helpers
{
    public static class IHtmlHelper_Extensions
    {
        public static string RenderedActionLink(this IHtmlHelper helper, string linkText, string actionName, string controllerName, object routeValues = null, object htmlAttributes = null)
        {
            // This isn't the most efficient way of rendering an ActionLink (see the discussion at https://github.com/aspnet/HtmlAbstractions/issues/35) but since this site isn't going to be high traffic AND I'm exporting it
            // to be hosted on GitHub Pages then it doesn't really matter
            var writer = new StringWriter();
            var content = helper.ActionLink(linkText, actionName, controllerName, routeValues, htmlAttributes);
            content.WriteTo(writer, HtmlEncoder.Default);
            return writer.ToString();
        }

        public static string RenderedActionLink(this IHtmlHelper helper, string actionName, string controllerName, object routeValues = null, object htmlAttributes = null)
        {
            return RenderedActionLink(helper, linkText: "", actionName, controllerName, routeValues, htmlAttributes);
        }
    }
}
