using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;
using FullTextIndexer.Core.Indexes.TernarySearchTree;

namespace Blog.Models
{
	public class SingleFolderPostRetriever : ISingleFolderPostRetriever
	{
		private DirectoryInfo _folder;
		public SingleFolderPostRetriever(DirectoryInfo folder)
		{
			if (folder == null)
				throw new ArgumentNullException("folder");
			_folder = folder;
		}

		/// <summary>
		/// This will never return null nor contain any null entries
		/// </summary>
		public NonNullImmutableList<Post> Get()
		{
			// The redirects set contains tuples From, To slugs (blank lines and those starting with a "#" are ignored, as are any that don't have any whitespace)
			const string redirectsFilename = "Redirects.txt";
			var redirectsFile = _folder.GetFiles(redirectsFilename).FirstOrDefault();
			IEnumerable<Tuple<string, string>> redirects;
			if (redirectsFile == null)
				redirects = new List<Tuple<string, string>>();
			else
			{
				redirects = readFileContents(redirectsFile)
					.Replace("\r\n", "\n")
					.Replace("\r", "\n")
					.Split('\n')
					.Select(entry => entry.Trim())
					.Where(entry => (entry != "") && !entry.StartsWith("#") && entry.Any(c => char.IsWhiteSpace(c)))
					.Select(entry => new string(entry.Select(c => char.IsWhiteSpace(c) ? ' ' : c).ToArray()))
					.Select(entry => entry.Split(new[] { ' ' }, 2))
					.Select(values => Tuple.Create(values[0], values[1]));
			}

			// The relatedPostRelationships set contains tuples of Post Id to Ids of related Posts (in the order that they should appear)
			const string relatedPostsFilename = "RelatedPosts.txt";
			var relatedPostsFile = _folder.GetFiles(relatedPostsFilename).FirstOrDefault();
			IEnumerable<Tuple<int, ImmutableList<int>>> relatedPostRelationships;
			if (relatedPostsFile == null)
				relatedPostRelationships = new List<Tuple<int, ImmutableList<int>>>();
			else
			{
				relatedPostRelationships = readFileContents(relatedPostsFile)
					.Replace("\r\n", "\n")
					.Replace("\r", "\n")
					.Split('\n')
					.Select(entry => entry.Trim())
					.Where(entry => (entry != "") && !entry.StartsWith("#") && entry.Contains(":"))
					.Select(entry =>
					{
						int sourcePostId;
						if (!int.TryParse(entry.Split(':').First(), out sourcePostId))
							return null;
						var relatedPostIds = entry.Split(':').Skip(1).First().Split(',')
							.Select(commaSeparatedValue =>
							{
								int relatedPostId;
								return int.TryParse(commaSeparatedValue, out relatedPostId) ? (int?)relatedPostId : null;
							})
							.Where(id => id != null)
							.Select(id => id.Value)
							.ToImmutableList();
						return relatedPostIds.Any() ? Tuple.Create(sourcePostId, relatedPostIds) : null;
					})
					.Where(entry => entry != null)
					.ToArray();
			}

			// We can use this functionality from the FullTextIndexer to generate the Post slug (it will replace accented characters, normalise whitespace,
			// remove punctuation and lower case the content - all we need to do then is replace spaces with hypens)
			var stringNormaliser = new DefaultStringNormaliser();
			var posts = new List<Post>();
			foreach (var file in _folder.EnumerateFiles("*.txt", SearchOption.TopDirectoryOnly))
			{
				if (file.Name.Equals(redirectsFilename, StringComparison.InvariantCultureIgnoreCase))
					continue;

				var fileSummary = tryToGetFileSummaryEntry(file.Name);
				if (fileSummary != null)
				{
					var fileContents = readFileContents(file);
					var title = tryToGetTitle(fileContents);
					if (title != null)
					{
						// 2014-09-17 DWR: Titles such as "C# State Machines" were being converted into "c-state-machines" which isn't as descriptive as
						// I'd like, "c-sharp-state-machines" is better. The replacement is done for "C#" and "F#" (a space is required after the
						// replacement content otherwise the "sharp" gets rolled into the next word)
						var slugBase = title.Replace("C#", "c sharp ").Replace("F#", "f sharp ");
						var slug = stringNormaliser.GetNormalisedString(slugBase).Replace(' ', '-');
						var redirectsForPost = new NonNullOrEmptyStringList(
							redirects.Where(r => r.Item2 == slug).Select(r => r.Item1)
						);

						// One this pass, set every tag's NumberOfPosts value to one since we don't have enough data to know better. After all of the
						// posts have been loaded, this can be fixed before the method terminates.
						posts.Add(new Post(
							fileSummary.Id,
							fileSummary.PostDate,
							file.LastWriteTime,
							slug,
							redirectsForPost,
							title,
							fileSummary.IsHighlight,
							fileContents,
							relatedPostRelationships.Any(r => r.Item1 == fileSummary.Id)
								? relatedPostRelationships.First(r => r.Item1 == fileSummary.Id).Item2
								: new ImmutableList<int>(),
							fileSummary.Tags.Select(tag => new TagSummary(tag, 1)).ToNonNullImmutableList()
						));
					}
				}
			}

			var tagCounts = posts
				.SelectMany(post => post.Tags)
				.Select(tag => tag.Tag)
				.GroupBy(tag => tag, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(groupedTag => groupedTag.Key, groupedTag => groupedTag.Count(), StringComparer.OrdinalIgnoreCase);
			return new NonNullImmutableList<Post>(posts.Select(post =>
				new Post(
					post.Id,
					post.Posted,
					post.LastModified,
					post.Slug,
					post.RedirectFromSlugs,
					post.Title,
					post.IsHighlight,
					post.MarkdownContent,
					post.RelatedPosts,
					post.Tags.Select(tag => new TagSummary(tag.Tag, tagCounts[tag.Tag])).ToNonNullImmutableList()
				)
			));
		}

		private string readFileContents(FileInfo file)
		{
			if (file == null)
				throw new ArgumentNullException("file");

			using (var stm = file.OpenText())
			{
				return stm.ReadToEnd();
			}
		}

		private string tryToGetTitle(string fileContents)
		{
			if (fileContents == null)
				throw new ArgumentNullException("fileContent");

			fileContents = fileContents.Trim();
			if (fileContents == "")
				return null;

			var breakPoint = fileContents.IndexOf("\n");
			if (breakPoint != -1)
				fileContents = fileContents.Substring(0, breakPoint - 1).Trim();

			while (fileContents.StartsWith("#"))
				fileContents = fileContents.Substring(1);

			return fileContents;
		}

		private FileSummaryEntry tryToGetFileSummaryEntry(string filename)
		{
			filename = (filename ?? "").Trim();
			if (filename == "")
				throw new ArgumentException("Null/empty file specified");

			if (!filename.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase))
				return null;

			filename = filename.Substring(0, filename.Length - 4);
			var segments = filename.Split(',');
			if (segments.Length < 8)
				return null;

			int id, year, month, date, hour, minute, second;
			if (!int.TryParse(segments[0], out id)
			|| !int.TryParse(segments[1], out year)
			|| !int.TryParse(segments[2], out month)
			|| !int.TryParse(segments[3], out date)
			|| !int.TryParse(segments[4], out hour)
			|| !int.TryParse(segments[5], out minute)
			|| !int.TryParse(segments[6], out second))
				return null;

			DateTime postDate;
			if (!DateTime.TryParse(String.Format("{0}-{1}-{2} {3}:{4}:{5}", year, month, date, hour, minute, second), out postDate))
				return null;

			bool isHighlight;
			if (segments[7] == "0")
				isHighlight = false;
			else if (segments[7] == "1")
				isHighlight = true;
			else
				return null;

			return new FileSummaryEntry(
				id,
				postDate,
				isHighlight,
				new NonNullOrEmptyStringList(
					segments.Skip(8).Select(t => t.Trim()).Where(t => t != "").Distinct()
				)
			);
		}

		private class FileSummaryEntry
		{
			public FileSummaryEntry(int id, DateTime postDate, bool isHighlight, NonNullOrEmptyStringList tags)
			{
				if (tags == null)
					throw new ArgumentNullException("tags");
				if (tags.Any(t => t.Trim() == ""))
					throw new ArgumentException("Blank tag specified");

				Id = id;
				PostDate = postDate;
				IsHighlight = isHighlight;
				Tags = tags;
			}
			public int Id { get; private set; }
			public DateTime PostDate { get; private set; }
			public NonNullOrEmptyStringList Tags { get; private set; }
			public bool IsHighlight { get; private set; }
		}
	}
}
