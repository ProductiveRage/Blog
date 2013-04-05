using System;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using Blog.Models;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Helpers.Sidebar
{
  public static class SidebarHelper
  {
    /// <summary>
    /// containerClassName is optional (may be empty or null) but title is not (must have a value), as must posts
    /// </summary>
    public static IHtmlString RenderPostStubList(
      this HtmlHelper helper,
      string title,
      string containerClassName,
      NonNullImmutableList<PostStub> posts,
      bool renderRSSLinkIfThereAreAnyPosts,
      bool renderEmptyContainer)
    {
      if (posts == null)
        throw new ArgumentNullException("posts");

      title = (title ?? "").Trim();
      if (title == "")
        throw new ArgumentException("Null/empty title specified");

      containerClassName = (containerClassName ?? "").Trim();
      if (containerClassName == "")
        containerClassName = null;

      var content = new StringBuilder();
      if (renderEmptyContainer || (posts.Count > 0))
      {
        if (containerClassName == null)
          content.Append("<div>");
        else
          content.AppendFormat("<div class=\"{0}\">", HttpUtility.HtmlAttributeEncode(containerClassName));
        content.AppendFormat("<h2>{0}</h2>", HttpUtility.HtmlEncode(title));
      }
      if (posts.Count > 0)
      {
        content.Append("<ul>");
        foreach (var post in posts)
          content.AppendFormat("<li>{0}</li>", helper.ActionLink(post.Title, "ArchiveById", "ViewPost", new { Id = post.Id }, null));
        content.Append("</ul>");
		if (renderRSSLinkIfThereAreAnyPosts)
		{
			content.Append("<div class=\"RSSFeedLink\">");
            content.Append(helper.ActionLink("RSS Feed", "Feed", "RSS"));
			content.Append("</div>");
		}
      }
      if (renderEmptyContainer || (posts.Count > 0))
        content.Append("</div>");
      return (IHtmlString)MvcHtmlString.Create(content.ToString());
    }

    /// <summary>
    /// containerClassName is optional (may be empty or null) but title is not (must have a value), as must posts
    /// </summary>
    public static IHtmlString RenderArchiveLinkList(
      this HtmlHelper helper,
      string title,
      string containerClassName,
      NonNullImmutableList<ArchiveMonthLink> archiveLinks,
      bool renderEmptyContainer)
    {
      if (archiveLinks == null)
        throw new ArgumentNullException("archiveLinks");

      title = (title ?? "").Trim();
      if (title == "")
        throw new ArgumentException("Null/empty title specified");

      containerClassName = (containerClassName ?? "").Trim();
      if (containerClassName == "")
        containerClassName = null;

      var content = new StringBuilder();
      if (renderEmptyContainer || (archiveLinks.Count > 0))
      {
        if (containerClassName == null)
          content.Append("<div>");
        else
          content.AppendFormat("<div class=\"{0}\">", HttpUtility.HtmlAttributeEncode(containerClassName));
        content.AppendFormat("<h2>{0}</h2>", HttpUtility.HtmlEncode(title));
      }
      if (archiveLinks.Count > 0)
      {
        content.Append("<ul>");
        foreach (var archiveLink in archiveLinks)
        {
          content.AppendFormat(
            "<li>{0}</li>",
            helper.ActionLink(
              archiveLink.DisplayText + " (" + archiveLink.PostCount + ")",
              "ArchiveByMonth",
			  "ViewPost",
              new { Month = archiveLink.Month, Year = archiveLink.Year },
			  null
            )
          );
        }
        content.Append("</ul>");
      }
      if (renderEmptyContainer || (archiveLinks.Count > 0))
        content.Append("</div>");
      return (IHtmlString)MvcHtmlString.Create(content.ToString());
    }
  }
}
