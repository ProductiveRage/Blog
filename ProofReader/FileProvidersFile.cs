using System;
using System.IO;
using Microsoft.Extensions.FileProviders;

namespace ProofReader
{
    internal sealed class FileProvidersFile : IFileInfo
    {
        private readonly FileInfo _file;
        public FileProvidersFile(FileInfo file) => _file = file;

        public bool Exists => _file.Exists;
        public bool IsDirectory => false;
        public DateTimeOffset LastModified => _file.LastWriteTimeUtc;
        public long Length => _file.Length;
        public string Name => _file.Name;
        public string PhysicalPath => _file.FullName;

        public Stream CreateReadStream() => _file.OpenRead();
    }
}