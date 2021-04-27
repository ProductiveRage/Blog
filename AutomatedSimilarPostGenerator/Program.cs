using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blog.Models;
using Microsoft.Extensions.FileProviders;

namespace AutomatedSimilarPostGenerator
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var postFolderPath = args.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(postFolderPath))
                throw new Exception("No folder path specified in arguments to locate blog posts");

            if (!Directory.Exists(postFolderPath))
                throw new Exception("Specified folder path does not exist: " + postFolderPath);

            var postRetriever = new SingleFolderPostRetriever(new DirectoryContents(postFolderPath));
            var posts = await postRetriever.Get();
            var suggestedRelatedContent = new StringBuilder();
            foreach (var (post, similar) in Recommender.GetSimilarPosts(posts))
            {
                Console.WriteLine();
                Console.WriteLine(post.Title);
                if (!similar.Any())
                {
                    Console.WriteLine("- No suggestions");
                    continue;
                }

                suggestedRelatedContent.Append(post.Id);
                suggestedRelatedContent.Append(':');
                var passedFirstSuggestion = false;
                foreach (var (otherPost, distance) in similar.OrderBy(otherPost => otherPost.Distance))
                {
                    Console.WriteLine($"{distance:0.000} {otherPost.Title}");
                    if (passedFirstSuggestion)
                        suggestedRelatedContent.Append(',');
                    suggestedRelatedContent.Append(otherPost.Id);
                    passedFirstSuggestion = true;
                }
                suggestedRelatedContent.AppendLine();
            }

            await File.WriteAllTextAsync(
                Path.Combine(postFolderPath, "AutoSuggestedRelatedPosts.txt"),
                suggestedRelatedContent.ToString()
            );
        }

        private sealed class DirectoryContents : IDirectoryContents
        {
            private readonly DirectoryInfo _folder;
            public DirectoryContents(string folderPath) => _folder = new DirectoryInfo(folderPath);

            public bool Exists => _folder.Exists;
            
            public IEnumerator<IFileInfo> GetEnumerator()
            {
                foreach (var file in _folder.EnumerateFiles())
                    yield return new File(file);
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            private sealed class File : IFileInfo
            {
                private readonly FileInfo _file;
                public File(FileInfo file) => _file = file;
                
                public bool Exists => _file.Exists;
                public bool IsDirectory => false;
                public DateTimeOffset LastModified => _file.LastWriteTimeUtc;
                public long Length => _file.Length;
                public string Name => _file.Name;
                public string PhysicalPath => _file.FullName;

                public Stream CreateReadStream() => _file.OpenRead();
            }
        }
    }
}
