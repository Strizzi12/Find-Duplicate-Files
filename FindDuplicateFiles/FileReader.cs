using System;
using System.Collections.Generic;
using System.IO;

namespace FindDuplicateFiles {
	public class FileReader {
		private readonly FileStream _stream;
		public readonly long FileSize;
		public string Path;

		private readonly Dictionary<int, FileItem> _readHashes = new Dictionary<int, FileItem>();

		public FileReader(string path, long fileSize) {
			Path = path;
			try {
				_stream = File.OpenRead(Path);
			} catch (Exception e) {
				Console.WriteLine(e.Message);
				_stream = null;
			}
			FileSize = fileSize;
		}

		public FileItem ReadSection(int index) {
			if (_stream == null) {
				return null;
			}
			FileItem result = null;
			if (_readHashes.TryGetValue(index, out result)) {
				return result;
			}

			var startIndex = 0;
			int bytecount = 0;
			bytecount = (int)Math.Pow(2, index);
			if (index > 0) {
				startIndex = (int)Math.Pow(2, index - 1);
			}
			if (FileSize / 2 < startIndex) {
				return null;
			}

			var fileItem = new FileItem();
			if ((FileSize / 2 + 0.5f) < (startIndex + bytecount)) {
				bytecount = (int)(Math.Ceiling((FileSize / 2.0) + 0.5f) - startIndex);
			}

			byte[] bytes = new byte[bytecount];
			_stream.Position = startIndex;
			_stream.Read(bytes, 0, bytecount);
			fileItem.Front = Helper.GetMurMurHash(bytes);

			byte[] bytes2 = new byte[bytecount];
			//_stream.Seek(0, SeekOrigin.Begin);
			_stream.Position = (FileSize - startIndex - bytecount);
			_stream.Read(bytes2, 0, bytecount);

			fileItem.Back = Helper.GetMurMurHash(bytes2);
			_readHashes.Add(index, fileItem);
			return fileItem;
		}
	}
}