using System;
using System.Web;
using System.Web.Mvc;
using HeyRed.MarkdownSharp;

namespace Blog.Helpers.Posts
{
	/// <summary>
	/// Helper class for transforming Markdown (based on Danny Tuppeny's code)
	/// </summary>
	public static class MarkdownHelper
	{
		/// <summary>
		/// Transforms a string of Markdown into HTML
		/// </summary>
		public static IHtmlString Markdown(this HtmlHelper helper, string text)
		{
			if (helper == null)
				throw new ArgumentNullException("text");
			if (text == null)
				throw new ArgumentNullException("text");

			// Return IHtmlString to prevent html elements being re-encoded when rendered
			return (IHtmlString)MvcHtmlString.Create(
				TransformIntoHtml(text)
			);
		}

		public static string TransformIntoHtml(string text)
		{
			if (text == null)
				throw new ArgumentNullException("text");

			return (new Markdown()).Transform(text);
		}
	}
}
