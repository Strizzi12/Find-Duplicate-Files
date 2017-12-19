using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FindDuplicateFiles {
	class FindDuplicateFiles {

		private static readonly string path = @"C:\Users\Andi\Downloads";
		private static readonly string path2 = @"C:\Users";
		

		static void Main(string[] args) {
			var dict = new ConcurrentDictionary<long, List<string>>();
			var controller = new Controller();
			var watch = System.Diagnostics.Stopwatch.StartNew();

			//Get all Files from directory and subdirectories 
			
			//TODO: Access restriction
			var files = controller.GetFilesForAllPaths();
			

			//Sort files by filesize into dictionary
			foreach (var filePath in files) {
				long fileSize = new FileInfo(filePath).Length;
				List<string> list;
				if (dict.TryGetValue(fileSize, out list)) {
					list.Add(filePath);
				} else {
					List<string> listTemp = new List<string>();
					listTemp.Add(filePath);
					dict.GetOrAdd(fileSize, listTemp);
				}
			}

			//Delete entries in dict where count == 1
			foreach (var toDelete in dict.Where(v => v.Value.Count == 1).ToList()) {
				List<string> list;
				dict.TryRemove(toDelete.Key, out list);
			}

			watch.Stop();
			var elapsedMs = watch.ElapsedMilliseconds;
			Console.WriteLine(elapsedMs + "ms");
			Console.ReadLine();
		}

		void ParseInputArguments(string[] argv) {
			
		}
		

		
	}
}