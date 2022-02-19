using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.FileProviders;

namespace ProofReader
{
    internal sealed class DirectoryContents : IDirectoryContents // TODO: Kill this in favour of SingleFolderPostRetriever taking a list of files?
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