using System;

namespace FindDuplicateFiles {
	public class FileItem {
		public string FileName { get; set; }
		public long Size { get; set; }
		public byte[] Front { get; set; }
		public byte[] Back { get; set; }

		public bool Equals(FileItem other) {
			if (Front == null || Back == null || other.Front == null || other.Back == null) {
				return false;
			}
			if (Front.Length != other.Front.Length || Back.Length != other.Back.Length) {
				return false;
			}
			for (var i = 0; i < Front.Length; i++) {
				if (Front[i] != other.Front[i]) {
					return false;
				}
			}
			for (var i = 0; i < Back.Length; i++) {
				if (Back[i] != other.Back[i]) {
					return false;
				}
			}
			return true;
		}
	}
}