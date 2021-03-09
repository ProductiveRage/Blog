using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization.Formatters.Binary;

namespace BlogBackEnd.FullTextIndexing.CachingPostIndexers
{
    public class FileBasedPostIndexCache : IPostIndexCache
	{
		private readonly FileInfo _dataFile;
		public FileBasedPostIndexCache(FileInfo dataFile)
		{
			if (dataFile == null)
				throw new ArgumentNullException("dataFile");

			_dataFile = dataFile;
		}

		/// <summary>
		/// This will return null if unable to deliver the data
		/// </summary>
		public CachedPostIndexContent TryToRetrieve()
		{
			try
			{
				_dataFile.Refresh();
				if (!_dataFile.Exists)
					return null;

                using var stream = File.Open(_dataFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var decompressedStream = new GZipStream(stream, CompressionMode.Decompress);
                return new BinaryFormatter().Deserialize(decompressedStream) as CachedPostIndexContent;
            }
			catch
			{
				// Ignore any errors - if access is denied then there's nothing we can do, just return null
				return null;
			}
		}

		/// <summary>
		/// If an entry already exists in the cache, it will be overwritten. It will throw an exception for a null data reference.
		/// </summary>
		public void Store(CachedPostIndexContent data)
		{
			if (data == null)
				throw new ArgumentNullException("data");

			try
			{
                using var memoryStream = new MemoryStream();
                using (var compressedStream = new GZipStream(memoryStream, CompressionMode.Compress))
                {
                    new BinaryFormatter().Serialize(compressedStream, data);
                }
                var serialisedData = memoryStream.ToArray();
                using var fileStream = File.Open(_dataFile.FullName, FileMode.Create);
                fileStream.Write(serialisedData, 0, serialisedData.Length);
            }
			catch
			{
				// Ignore any errors - if access is denied then there's nothing we can do, just push on
			}
		}
	}
}
