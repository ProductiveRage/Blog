using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blog.Misc;
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

            var postRetriever = new SingleFolderPostRetriever(
                new DirectoryInfo(postFolderPath)
                    .EnumerateFiles()
                    .Select(f => new WebFileInfoFromDisk(f)));
            var posts = await postRetriever.Get();
            var suggestedRelatedContent = new StringBuilder();
            foreach (var (post, similar) in (await Recommender.GetSimilarPosts(posts)).OrderBy(result => result.Post.Id))
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
                foreach (var (otherPost, similarityDistance, proximityByTitleTFIDF) in similar.OrderBy(otherPost => otherPost.SimilarityDistance))
                {
                    Console.WriteLine($"{similarityDistance:0.000} {otherPost.Title}");
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
    }
}
