using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FindDuplicateFiles {
	public class Controller {
		private bool _waitForTermination;
		private bool _printProcessTime;
		private bool _optimizeTaskCount;
		private bool _error;
		public List<string> _filePaths = new List<string>();
		private bool _moreInfo;
		private List<string> _fileFilter = new List<string>(); //already a regex pattern
		private int? _depthOfRecursion = null;
		public int? _maxTasks = null;

		public List<string> SlowFindDuplicateFiles(List<string> files) {
			return files.Select(
					f => new {
						FileName = f,
						FileHash = Encoding.UTF8.GetString(
							new SHA1Managed().ComputeHash(new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
					}).CatchExceptions((ex) => Console.WriteLine(ex.Message))
				.GroupBy(f => f.FileHash)
				.Select(g => new { FileHash = g.Key, Files = g.Select(z => z.FileName).ToList() })
				.SelectMany(f => f.Files.Skip(1))
				.ToList();
		}
		
		public void ShowDuplicateFiles(ConcurrentBag<List<FileReader>> bag) {
			Console.WriteLine("Duplicate files:");
			foreach (var list in bag) {
				foreach (var item in list) {
					Console.WriteLine($"File: {item.Path}");
				}
				Console.WriteLine();
			}
		}

		public ConcurrentBag<List<FileReader>> FindDuplicateFiles(ConcurrentDictionary<long, List<string>> dictionary) {
			var duplicates = new ConcurrentBag<List<FileReader>>();
			Parallel.ForEach(dictionary, _maxTasks != null ? new ParallelOptions { MaxDegreeOfParallelism = _maxTasks.Value } : new ParallelOptions(),
				dict => {
					var readers = dict.Value.Select(i => new FileReader(i, dict.Key)).ToList();
					for (int i = 0; i < readers.Count - 1; i++) {
						var currentGroup = new List<FileReader>();
						currentGroup.Add(readers[i]);

						for (int j = i + 1; j < readers.Count; j++) {
							var current = readers[i];
							var other = readers[j];
							if (Compare(current, other)) {
								currentGroup.Add(other);
							}
						}
						if (currentGroup.Count > 1) {
							duplicates.Add(currentGroup);
						}
						readers.RemoveAll(x => currentGroup.Any(y => x == y));
					}
				});
			return duplicates;
		}

		private bool Compare(FileReader cur, FileReader other) {
			var equal = true;
			for (var index = 0; equal; index++) {
				var itemCur = cur.ReadSection(index);
				var itemOther = other.ReadSection(index);
				if (itemCur == null || itemOther == null) {
					break;
				}
				equal = itemCur.Equals(itemOther);
			}
			return equal;
		}

		public List<string> GetFilesForAllPaths() {
			List<string> allFiles = new List<string>();
			Parallel.ForEach(_filePaths, _maxTasks != null ? new ParallelOptions { MaxDegreeOfParallelism = _maxTasks.Value } : new ParallelOptions(),
				filePath => {
					var list = GetFilesFromPath(filePath, _fileFilter, _depthOfRecursion);
					allFiles.AddRange(list);
				});
			return allFiles;
		}

		private List<string> GetFilesFromPath(string path, List<string> filter, int? depth = null) {
			var files = new List<string>();
			string[] directoriesInPath;
			string[] filesInPath;
			try {
				directoriesInPath = Directory.GetDirectories(path); //Known bug
				filesInPath = Directory.GetFiles(path);
			} catch (Exception ex) {
				Console.WriteLine(ex.Message);
				return new List<string>();
			}
			foreach (var directory in directoriesInPath) {
				var temp = new List<string>();
				if (depth != null && depth > 0) {
					temp = GetFilesFromPath(directory, filter, depth - 1);
				} else {
					temp = GetFilesFromPath(directory, filter);
				}
				files.AddRange(temp);
			}
			files.AddRange(filesInPath);
			return FilterFiles(files, filter);
		}

		private List<string> FilterFiles(List<string> files, List<string> filters) {
			var list = new List<string>();
			if (filters.Count > 0) {
				Parallel.ForEach(files, _maxTasks != null ? new ParallelOptions { MaxDegreeOfParallelism = _maxTasks.Value } : new ParallelOptions(),
					file => {
						foreach (var filter in filters) {
							var regex = new Regex(filter);
							if (regex.IsMatch(file)) {
								list.Add(file);
								break;
							}
						}
					});
				return list;
			}
			return files;
		}

		public void ParseInputArguments(string[] args) {
			for (int i = 0; i < args.Length; i++) {
				switch (args[i]) {
					case "-w":
						_waitForTermination = true;
						continue;
					case "-p":
						_printProcessTime = true;
						continue;
					case "-t":
						if (args[i + 1] != null) {
							_optimizeTaskCount = true; //When file size is known, a calculation of the optimal thread count could be made.
							_maxTasks = null;
							continue;
						}
						if (Helper.IsDigitsOnly(args[i + 1])) {
							_maxTasks = Int32.Parse(args[i + 1]);
							i++; //Counter can be increased because the value of maxThreads is already read.
							continue;
						} else {
							Console.WriteLine("Error! Max. thread count must not contain character");
							break;
						}
					case "-h":
						PrintHelp();
						continue;
					case "-v":
						_moreInfo = true;
						continue;
					case "-f":
						if (args[i + 1] != null) {
							var completeString = args[i + 1];
							//Split string at ;
							var allFilters = completeString.Split(';');
							foreach (var filter in allFilters) {
								var temp = filter.Replace("*", @"([a-zA-Z0-9\._\-]*)");
								_fileFilter.Add(temp);
							}
							i++; //Counter can be increased because the value of fileFilter is already read.
							continue;
						} else {
							Console.WriteLine("Error! No filter given");
							break;
						}
					case "-s":
						_filePaths.Add(args[i + 1]);
						i++; //Counter can be increased because the value of filePath is already read.
						continue;
					case "-r":
						if (args[i + 1] != null && !args[i + 1].Contains("-")
						) //Check if the next input argument is another functionality.
						{
							_depthOfRecursion = null; //All files and folders are progressed
							continue;
						} else {
							var help = args[i + 1];
							if (Helper.IsDigitsOnly(help)) {
								_depthOfRecursion = Int32.Parse(help);
								i++; //Counter can be increased because the value of maxThreads is already read.
								continue;
							}
							Console.WriteLine("Error! Folder depth must not contain character.");
							break;
						}
					default:
						Console.WriteLine("Error! Unknown argument detected.");
						PrintHelp();
						break;
				}
			}
		}

		private static void PrintHelp() {
			Console.WriteLine("Die Applikation kann wie folgt aufzurufen:");
			Console.WriteLine("cntFileBits [-r [n]] [-f fileFilter] [-t maxThreads] [-h] [-p] [-v] [-w] [-s startPath]");
			Console.WriteLine("-s startPath	Gibt das Startverzeichnis an, ab dem die Dateien gelesen werden sollen;");
			Console.WriteLine("die Option -s kann auch mehrfach angegeben werden, z.B. wenn zwei Partitionen durchsucht werden sollen");
			Console.WriteLine("-r [n] Rekursives Lesen der Unterverzeichnisse; wenn n (bei n >= 1) angegeben, dann");
			Console.WriteLine("bestimmt n die Tiefe der Rekursion; wird n nicht angegeben, dann werden");
			Console.WriteLine("rekursiv alle unter dem Startverzeichnis stehenden Verzeichnisse und deren Dateien gelesen;");
			Console.WriteLine("-f fileFilter fileFilter gibt an, welche Dateien gelesen werden sollen; z.B. *.iso oder bild*.jpg;");
			Console.WriteLine("wird diese Option nicht angegeben, so werden alle Dateien gelesen;");
			Console.WriteLine("-t maxThreads maximale Anzahl der Threads; wird diese Option nicht angegeben, dann wird die Anzahl der Threads automatisch optimiert.");
			Console.WriteLine("-h Anzeige der Hilfe & Copyright Info; wird automatisch angezeigt, wenn beim Programmstart keinen Option angegeben wird.");
			Console.WriteLine("-p Ausgabe der Prozesserungszeit auf stdout in Sekunden.Millisekunden");
			Console.WriteLine("-v Erweiterte Ausgabe etwaiger Prozessierungsinformationen auf stdout");
			Console.WriteLine("-w Warten auf eine Taste unmittelbar bevor die applikation terminiert.");
			Console.WriteLine("Copyright© by Mike Thomas and Andreas Reschenhofer");
		}


	}
}

//public ConcurrentDictionary<long, List<string>> FindDuplicateFiles(ConcurrentDictionary<long, List<string>> dictionary) {
//	var duplicates = new ConcurrentDictionary<long, List<string>>();
//	Parallel.ForEach(dictionary, _maxTasks != null ? new ParallelOptions { MaxDegreeOfParallelism = _maxTasks.Value } : new ParallelOptions(),
//		dict => {

//			int lengthToCalculate = 1;
//			var groupedDuplicates = new ConcurrentDictionary<Tuple<byte[], byte[]>, List<string>>();

//			while (dict.Key > lengthToCalculate) {
//				var fileItems = new List<FileItem>();
//				var fileList = dict.Value;

//				fileItems = CalculateQuickHashes(fileList, lengthToCalculate);

//				//Check for duplicates
//				for (int i = 0; i < fileItems.Count - 1; i++) {
//					for (int j = i + 1; j < fileItems.Count; j++) {
//						var firstHash1 = fileItems[i].Front;
//						var lastHash1 = fileItems[i].Back;

//						var firstHash2 = fileItems[j].Front;
//						var lastHash2 = fileItems[j].Back;
//						if (CompareHashes(firstHash1, firstHash2) && CompareHashes(lastHash1, lastHash2)) {
//							//Das bedeutet, file von i und file von j sind gleich.
//						}
//					}
//				}

//				lengthToCalculate *= 2;
//			}

//		});
//	return duplicates;
//}

//private List<FileItem> CalculateQuickHashes(List<string> fileList, int lengthToCalculate) {
//	var fileItems = new List<FileItem>();
//	//Calculate Quick Hashes
//	foreach (var file in fileList) {
//		using (var stream = File.OpenRead(file)) {
//			var fileItem = new FileItem {
//				FileName = file,
//				Size = file.Length
//			};

//			//first byte
//			if (file.Length > 0) {
//				if (file.Length >= lengthToCalculate) {
//					byte[] bytes = new byte[lengthToCalculate];
//					stream.Read(bytes, 0, lengthToCalculate);
//					fileItem.Front = Helper.GetMurMurHash(bytes);

//					byte[] bytes2 = new byte[lengthToCalculate];
//					stream.Read(bytes2, file.Length - lengthToCalculate, lengthToCalculate);
//					fileItem.Back = Helper.GetMurMurHash(bytes2);

//				} else {
//					byte[] bytes = new byte[lengthToCalculate];
//					stream.Read(bytes, 0, lengthToCalculate);
//					fileItem.Front = Helper.GetMurMurHash(bytes);
//					fileItem.Back = null;
//				}
//			}
//			fileItems.Add(fileItem);
//		}
//	}
//	return fileItems;
//}

//private static bool CompareHashes(byte[] hash1, byte[] hash2) {
//	if (hash1.Length != hash2.Length) {
//		return false;
//	} else {
//		for (int i = 0; i < hash1.Length; i++) {
//			if (hash1[i] != hash2[i]) {
//				return false;
//			}
//		}
//		return true;
//	}
//}