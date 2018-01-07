using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FindDuplicateFiles {
	public class Controller {
		private bool WaitForTermination { get; set; }
		public bool PrintProcessTime { get; set; }
		private bool MoreInfo { get; set; }
		private int? DepthOfRecursion { get; set; }
		private int? MaxTasks { get; set; }

		private readonly List<string> FilePaths = new List<string>();
		private readonly List<string> FileFilter = new List<string>(); //already contains the complete regex pattern

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

		public void ShowDuplicateFiles(ConcurrentDictionary<long, List<FileReader>> dict) {
			Console.WriteLine("Duplicate files");
			foreach (var item in dict) {
				Console.WriteLine($"File size: {item.Key}");
				foreach (var entry in item.Value) {
					Console.WriteLine($"File: {entry.Path}");
				}
				Console.WriteLine();
			}
			int anzDups = dict.Values.Sum(list => list.Count);
			Console.WriteLine($"Count of duplicate files: {anzDups}");
		}

		public ConcurrentDictionary<long, List<FileReader>> FindDuplicateFiles(ConcurrentDictionary<long, List<string>> dictionary) {
			var duplicates = new ConcurrentDictionary<long, List<FileReader>>();
			Parallel.ForEach(dictionary, MaxTasks != null ? new ParallelOptions { MaxDegreeOfParallelism = MaxTasks.Value } : new ParallelOptions(),
				dict => {
					var readers = dict.Value.Select(i => new FileReader(i, dict.Key)).ToList();     //.Where(x => x.FileSize > 0).ToList();
					for (int i = 0; i < readers.Count - 1; i++) {
						var currentGroup = new List<FileReader> { readers[i] };

						for (int j = i + 1; j < readers.Count; j++) {
							var current = readers[i];
							if (MoreInfo) {
								MyPrint($"Processing file: {current.Path}");
							}
							var other = readers[j];
							if (Compare(current, other)) {
								currentGroup.Add(other);
							}
						}

						if (currentGroup.Count > 1) {
							duplicates.TryAdd(readers.FirstOrDefault().FileSize, currentGroup);
						}
						readers.RemoveAll(x => currentGroup.Any(y => x == y));
					}

					//Memory Management
					foreach (var reader in readers) {
						reader.Dispose();
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
			Parallel.ForEach(FilePaths, MaxTasks != null ? new ParallelOptions { MaxDegreeOfParallelism = MaxTasks.Value } : new ParallelOptions(),
				filePath => {
					var list = GetFilesFromPath(filePath, FileFilter, DepthOfRecursion);
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
			if (filters.Count == 0) {
				return files;
			}
			foreach (var file in files) {
				if (file != null) {
					foreach (var filter in filters) {
						var regex = new Regex(filter);
						if (regex.IsMatch(file)) {
							list.Add(file);
							break;
						}
					}
				}
			}
			return list;
		}

		public void Terminate() {
			if (WaitForTermination) {
				Console.ReadLine();
			}
		}

		public void MyPrint(string input) {
			if (MoreInfo) {
				Console.WriteLine(input);
			}
		}

		public void ParseInputArguments(string[] args) {
			for (int i = 0; i < args.Length; i++) {
				switch (args[i]) {
					case "-w":
						WaitForTermination = true;
						continue;
					case "-p":
						PrintProcessTime = true;
						continue;
					case "-t":
						if (args[i + 1] != null) {
							MaxTasks = null;
							continue;
						}
						if (Helper.IsDigitsOnly(args[i + 1])) {
							MaxTasks = Int32.Parse(args[i + 1]);
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
						MoreInfo = true;
						continue;
					case "-f":
						if (args[i + 1] != null) {
							var completeString = args[i + 1];
							//Split string at ;
							var allFilters = completeString.Split(';');
							foreach (var filter in allFilters) {
								var temp = filter.Replace("*", @"([a-zA-Z0-9\._\-]*)");
								FileFilter.Add(temp);
							}
							i++; //Counter can be increased because the value of fileFilter is already read.
							continue;
						} else {
							Console.WriteLine("Error! No filter given");
							break;
						}
					case "-s":
						FilePaths.Add(args[i + 1]);
						i++; //Counter can be increased because the value of filePath is already read.
						continue;
					case "-r":
						if (args[i + 1] != null && !args[i + 1].Contains("-")
						) //Check if the next input argument is another functionality.
						{
							DepthOfRecursion = null; //All files and folders are progressed
							continue;
						} else {
							var help = args[i + 1];
							if (Helper.IsDigitsOnly(help)) {
								DepthOfRecursion = Int32.Parse(help);
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
			Console.WriteLine();
			Console.WriteLine("searchDubs [-r [n]] [-f fileFilter] [-t maxThreads] [-h] [-p] [-v] [-w] [-s startPath]");
			Console.WriteLine();
			Console.WriteLine("-s startPath Gibt das Startverzeichnis an, ab dem die Dateien gelesen werden sollen;");
			Console.WriteLine("             die Option -s kann auch mehrfach angegeben werden, z.B. wenn zwei Partitionen durchsucht werden sollen");
			Console.WriteLine("-r [n]       Rekursives Lesen der Unterverzeichnisse; wenn n (bei n >= 1) angegeben, dann");
			Console.WriteLine("             bestimmt n die Tiefe der Rekursion; wird n nicht angegeben, dann werden");
			Console.WriteLine("             rekursiv alle unter dem Startverzeichnis stehenden Verzeichnisse und deren Dateien gelesen;");
			Console.WriteLine("-f           fileFilter fileFilter gibt an, welche Dateien gelesen werden sollen; z.B. *.iso oder bild*.jpg;");
			Console.WriteLine("             wird diese Option nicht angegeben, so werden alle Dateien gelesen;");
			Console.WriteLine("-t           maxThreads maximale Anzahl der Threads; wird diese Option nicht angegeben, dann wird die Anzahl der Threads automatisch optimiert.");
			Console.WriteLine("-h           Anzeige der Hilfe & Copyright Info; wird automatisch angezeigt, wenn beim Programmstart keinen Option angegeben wird.");
			Console.WriteLine("-p           Ausgabe der Prozesserungszeit auf stdout in Sekunden.Millisekunden");
			Console.WriteLine("-v           Erweiterte Ausgabe etwaiger Prozessierungsinformationen auf stdout");
			Console.WriteLine("-w           Warten auf eine Taste unmittelbar bevor die applikation terminiert.");
			Console.WriteLine();
			Console.WriteLine("Copyright© by Mike Thomas and Andreas Reschenhofer");
		}
	}
}