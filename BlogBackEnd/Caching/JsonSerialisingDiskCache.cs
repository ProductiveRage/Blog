using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Caching;
using Newtonsoft.Json;

namespace BlogBackEnd.Caching
{
	/// <summary>
	/// This will cache data indefinitely, there is no cache expiration time
	/// </summary>
	public sealed class JsonSerialisingDiskCache : ICache
	{
		private static readonly Encoding _fileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
		private static readonly HashSet<char> _illegalCharacters = new HashSet<char>(Path.GetInvalidFileNameChars());

		private readonly DirectoryInfo _cacheFolder;
		private readonly bool _swallowAnyFileAccessExceptions;
		public JsonSerialisingDiskCache(DirectoryInfo cacheFolder, bool swallowAnyFileAccessExceptions)
		{
			if (cacheFolder == null)
				throw new ArgumentNullException("cacheFolder");

			_cacheFolder = cacheFolder;
			_swallowAnyFileAccessExceptions = swallowAnyFileAccessExceptions;
		}

		/// <summary>
		/// The getter will return null if there is no cached data matching the specified key. The setter will only write the data if the key is not already present in the data
		/// (so that the cache can implement its own expiration handling and callers can make push requests to the data without having to worry about checking whether it's already
		/// there or not - if a caller really wants to overwrite any present data, the Remove method may be called first). Both getter and setter will throw an exception for a null
		/// or empty key. The setter will throw an exception if a null value is specified.
		/// </summary>
		public object this[string key]
		{
			get
			{
				if (string.IsNullOrWhiteSpace(key))
					throw new ArgumentException("Null/blank key specified");

				try
				{
					_cacheFolder.Refresh();
					if (!_cacheFolder.Exists)
						return null;
				}
				catch { }

				var file = TryToGetCacheFile(key);
				if (file == null)
					return null; // The key can not be stored so this cache layer won't be able to return any data

				try
				{
					if (!file.Exists)
						return null;
					return JsonConvert.DeserializeObject(
						_fileEncoding.GetString(File.ReadAllBytes(file.FullName))
					);
				}
				catch
				{
					if (!_swallowAnyFileAccessExceptions)
						throw;
					return null;
				}
			}
			set
			{
				if (string.IsNullOrWhiteSpace(key))
					throw new ArgumentException("Null/blank key specified");

				var file = TryToGetCacheFile(key);
				if (file == null)
					return; // The key can not be stored so this cache layer won't be able to store the data

				try
				{
					_cacheFolder.Refresh();
					if (!_cacheFolder.Exists)
						_cacheFolder.Create();

					File.WriteAllBytes(
						file.FullName,
						_fileEncoding.GetBytes(JsonConvert.SerializeObject(value))
					);
				}
				catch
				{
					if (!_swallowAnyFileAccessExceptions)
						throw;
				}
			}
		}

		/// <summary>
		/// This will do nothing if the key is not present in the cache
		/// </summary>
		public void Remove(string key)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("Null/empty cacheKey specified");

			var file = TryToGetCacheFile(key);
			if (file == null)
				return; // The key can not be stored so this cache layer won't have stored any data to remove

			try
			{
				if (file.Exists)
					file.Delete();
			}
			catch
			{
				if (!_swallowAnyFileAccessExceptions)
					throw;
			}
		}

		public void RemoveAll()
		{
			try
			{
				_cacheFolder.Refresh();
				if (!_cacheFolder.Exists)
					return;

				foreach (var file in _cacheFolder.EnumerateFiles())
				{
					try
					{
						file.Delete();
					}
					catch
					{
						if (!_swallowAnyFileAccessExceptions)
							throw;
					}
				}
			}
			catch
			{
				if (!_swallowAnyFileAccessExceptions)
					throw;
			}
		}

		/// <summary>
		/// This will return null if a FileInfo instance can not be generated for it (if it would result in a path that is too long to store, for example)
		/// </summary>
		private FileInfo TryToGetCacheFile(string key)
		{
			if (string.IsNullOrWhiteSpace(key))
				throw new ArgumentException("Null/blank key specified");

			var filename = new string(key.Select(c => _illegalCharacters.Contains(c) ? '-' : c).ToArray()) + ".json";
			try
			{
				// This will throw if the path would be too long
				return new FileInfo(Path.Combine(_cacheFolder.FullName, filename));
			}
			catch
			{
				return null;
			}
		}
	}
}
