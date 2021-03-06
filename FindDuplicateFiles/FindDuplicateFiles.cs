﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FindDuplicateFiles {
	class FindDuplicateFiles {

		//private static readonly string path = @"C:\Users\Andi\Downloads";
		//private static readonly string path2 = @"C:\Users";

		static void Main(string[] args) {
			var dict = new ConcurrentDictionary<long, List<string>>();
			var controller = new Controller();

			controller.ParseInputArguments(args);

			var watch = Stopwatch.StartNew();
			//controller._filePaths.Add(path2);

			//Get all Files from directory and subdirectories 
			var files = controller.GetFilesForAllPaths();

			//Sort files by filesize into dictionary
			foreach (var filePath in files) {
				long fileSize;
				try {
					fileSize = new FileInfo(filePath).Length;
				} catch (Exception ex) {
					Console.WriteLine(ex.Message);
					continue;
				}
				if (dict.TryGetValue(fileSize, out var list)) {
					list.Add(filePath);
				} else {
					List<string> listTemp = new List<string> { filePath };
					dict.GetOrAdd(fileSize, listTemp);
				}
			}

			//Delete entries in dict where count == 1
			foreach (var toDelete in dict.Where(v => v.Value.Count == 1).ToList()) {
				dict.TryRemove(toDelete.Key, out _);
			}

			var dups = controller.FindDuplicateFiles(dict);
			watch.Stop();

			controller.ShowDuplicateFiles(dups);
			Console.WriteLine($"Number of checked files: {files.Count}");
			var elapsedMs = watch.ElapsedMilliseconds;
			if (controller.PrintProcessTime) {
				Console.WriteLine("Execution time = " + elapsedMs + " ms");
			}
			controller.Terminate();
		}
	}
}