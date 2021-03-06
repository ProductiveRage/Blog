﻿using System;
using System.Text;
using System.Web;
using Blog.Models;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Blog.Helpers
{
    public static class SidebarHelper
    {
        /// <summary>
        /// containerClassName is optional (may be empty or null) but title is not (must have a value), as must posts
        /// </summary>
        public static HtmlString RenderPostStubList(
            this IHtmlHelper helper,
            string title,
            string containerClassName,
            NonNullImmutableList<PostStub> posts,
            bool renderRSSLinkIfThereAreAnyPosts,
            bool renderEmptyContainer)
        {
            if (posts == null)
                throw new ArgumentNullException(nameof(posts));

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
                    content.AppendFormat("<li>{0}</li>", helper.RenderedActionLink(post.Title, "ArchiveBySlug", "ViewPost", new { post.Slug }, null));
                content.Append("</ul>");
                if (renderRSSLinkIfThereAreAnyPosts)
                {
                    content.Append("<div class=\"RSSFeedLink\">");
                    content.Append(helper.RenderedActionLink("RSS Feed", "Feed", "RSS"));
                    content.Append("</div>");
                }
            }
            if (renderEmptyContainer || (posts.Count > 0))
                content.Append("</div>");
            return new HtmlString(content.ToString());
        }

        /// <summary>
        /// containerClassName is optional (may be empty or null) but title is not (must have a value), as must posts
        /// </summary>
        public static HtmlString RenderArchiveLinkList(this IHtmlHelper helper, string title, string containerClassName, NonNullImmutableList<ArchiveMonthLink> archiveLinks, bool renderEmptyContainer)
        {
            if (archiveLinks == null)
                throw new ArgumentNullException(nameof(archiveLinks));

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
                      helper.RenderedActionLink(
                        archiveLink.DisplayText + " (" + archiveLink.PostCount + ")",
                        "ArchiveByMonth",
                        "ViewPost",
                        new { archiveLink.Month, archiveLink.Year },
                        null
                      )
                    );
                }
                content.Append("</ul>");
                content.Append("<div class=\"EveryTitle\">");
                content.Append(
                    helper.RenderedActionLink(
                        "Every Post Title",
                        "ArchiveByTitle",
                        "ViewPost"
                    )
                );
                content.Append("</div>");
            }
            if (renderEmptyContainer || (archiveLinks.Count > 0))
                content.Append("</div>");
            return new HtmlString(content.ToString());
        }
    }
}
