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
						var slug = stringNormaliser.GetNormalisedString(title).Replace(' ', '-');
						var redirectsForPost = new NonNullOrEmptyStringList(
							redirects.Where(r => r.Item2 == slug).Select(r => r.Item1)
						);
						posts.Add(new Post(
						  fileSummary.Id,
						  fileSummary.PostDate,
						  file.LastWriteTime,
						  slug,
						  redirectsForPost,
						  title,
						  fileSummary.IsHighlight,
						  fileContents,
						  fileSummary.RelatedPostIds,
						  fileSummary.Tags
						));
					}
				}
			}
			return new NonNullImmutableList<Post>(posts);
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

			var relatedPostIds = new List<int>();
			var tags = new List<string>();
			foreach (var relatedPostIdOrTag in segments.Skip(8))
			{
				int relatedPostId;
				if (int.TryParse(relatedPostIdOrTag, out relatedPostId))
					relatedPostIds.Add(relatedPostId);
				else
					tags.Add(relatedPostIdOrTag);
			}

			return new FileSummaryEntry(
				id,
				postDate,
				isHighlight,
				relatedPostIds.ToImmutableList(),
				new NonNullOrEmptyStringList(
					tags.Select(t => t.Trim()).Where(t => t != "").Distinct()
				)
			);
		}

		private class FileSummaryEntry
		{
			public FileSummaryEntry(int id, DateTime postDate, bool isHighlight, ImmutableList<int> relatedPostIds, NonNullOrEmptyStringList tags)
			{
				if (relatedPostIds == null)
					throw new ArgumentNullException("relatedPostIds");
				if (tags == null)
					throw new ArgumentNullException("tags");
				if (tags.Any(t => t.Trim() == ""))
					throw new ArgumentException("Blank tag specified");

				Id = id;
				PostDate = postDate;
				IsHighlight = isHighlight;
				RelatedPostIds = relatedPostIds;
				Tags = tags;
			}
			public int Id { get; private set; }
			public DateTime PostDate { get; private set; }
			public ImmutableList<int> RelatedPostIds { get; private set; }
			public NonNullOrEmptyStringList Tags { get; private set; }
			public bool IsHighlight { get; private set; }
		}
	}
}
