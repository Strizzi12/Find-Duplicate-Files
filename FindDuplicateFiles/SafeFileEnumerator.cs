using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FindDuplicateFiles
{

  /// <summary>
  /// Solution for: https://connect.microsoft.com/VisualStudio/feedback/details/512171/directory-enumeratedirectory-etc-unusable-due-to-frequent-unauthorizedaccessexceptions-even-runas-administrator#tabs
  /// Used: https://stackoverflow.com/questions/9746538/fastest-safest-file-finding-parsing/9758932#9758932
  /// </summary>
  public class SafeFileEnumerator : IEnumerable<FileSystemInfo>
  {
    /// <summary>
    /// Starting directory to search from
    /// </summary>
    private DirectoryInfo root;

    /// <summary>
    /// Filter pattern
    /// </summary>
    private string pattern;

    /// <summary>
    /// Indicator if search is recursive or not
    /// </summary>
    private SearchOption searchOption;

    /// <summary>
    /// Any errors captured
    /// </summary>
    private IList<Exception> errors;

    /// <summary>
    /// Max number of depth the Enumerator will go when SearchOption == AllSubDirectories
    /// </summary>
    private int? depth = null;

    /// <summary>
    /// Create an Enumerator that will scan the file system, skipping directories where access is denied
    /// </summary>
    /// <param name="root">Starting Directory</param>
    /// <param name="pattern">Filter pattern</param>
    /// <param name="option">Recursive or not</param>
    public SafeFileEnumerator(string root, string pattern, SearchOption option, int? depth = null)
        : this(new DirectoryInfo(root), pattern, option, depth)
    { }

    /// <summary>
    /// Create an Enumerator that will scan the file system, skipping directories where access is denied
    /// </summary>
    /// <param name="root">Starting Directory</param>
    /// <param name="pattern">Filter pattern</param>
    /// <param name="option">Recursive or not</param>
    public SafeFileEnumerator(DirectoryInfo root, string pattern, SearchOption option, int? depth = null)
        : this(root, pattern, option, new List<Exception>(), depth)
    { }

    // Internal constructor for recursive itterator
    private SafeFileEnumerator(DirectoryInfo root, string pattern, SearchOption option, IList<Exception> errors, int? depth = null)
    {
      if (root == null || !root.Exists)
      {
        throw new ArgumentException("Root directory is not set or does not exist.", "root");
      }
      this.root = root;
      this.searchOption = option;
      this.pattern = String.IsNullOrEmpty(pattern)
          ? "*"
          : pattern;
      this.errors = errors;
      this.depth = depth;
    }

    /// <summary>
    /// Errors captured while parsing the file system.
    /// </summary>
    public Exception[] Errors
    {
      get
      {
        return errors.ToArray();
      }
    }

    /// <summary>
    /// Helper class to enumerate the file system.
    /// </summary>
    private class Enumerator : IEnumerator<FileSystemInfo>
    {
      // Core enumerator that we will be walking though
      private IEnumerator<FileSystemInfo> fileEnumerator;
      // Directory enumerator to capture access errors
      private IEnumerator<DirectoryInfo> directoryEnumerator;

      private DirectoryInfo root;
      private string pattern;
      private SearchOption searchOption;
      private IList<Exception> errors;
      private int? depth = null;

      public Enumerator(DirectoryInfo root, string pattern, SearchOption option, IList<Exception> errors, int? depth = null)
      {
        this.root = root;
        this.pattern = pattern;
        this.errors = errors;
        this.searchOption = option;
        this.depth = depth;

        Reset();
      }

      /// <summary>
      /// Current item the primary itterator is pointing to
      /// </summary>
      public FileSystemInfo Current
      {
        get
        {
          //if (fileEnumerator == null) throw new ObjectDisposedException("FileEnumerator");
          return fileEnumerator.Current as FileSystemInfo;
        }
      }

      object System.Collections.IEnumerator.Current
      {
        get { return Current; }
      }

      public void Dispose()
      {
        Dispose(true, true);
      }

      private void Dispose(bool file, bool dir)
      {
        if (file)
        {
          if (fileEnumerator != null)
            fileEnumerator.Dispose();

          fileEnumerator = null;
        }

        if (dir)
        {
          if (directoryEnumerator != null)
            directoryEnumerator.Dispose();

          directoryEnumerator = null;
        }
      }

      public bool MoveNext()
      {
        // Enumerate the files in the current folder
        if ((fileEnumerator != null) && (fileEnumerator.MoveNext()))
          return true;

        // Don't go recursive...
        if (searchOption == SearchOption.TopDirectoryOnly) { return false; }

        while ((directoryEnumerator != null) && (directoryEnumerator.MoveNext()))
        {
          Dispose(true, false);

          if (depth <= 0)
          {
            Dispose(true, true);
            return false;
          }

          try
          {
            fileEnumerator = new SafeFileEnumerator(
                directoryEnumerator.Current,
                pattern,
                SearchOption.AllDirectories,
                errors,
                depth-1 //dec. of depth which he can go further in
                ).GetEnumerator();
          }
          catch (Exception ex)
          {
            errors.Add(ex);
            continue;
          }

          // Open up the current folder file enumerator
          if (fileEnumerator.MoveNext())
            return true;
        }

        Dispose(true, true);

        return false;
      }

      public void Reset()
      {
        Dispose(true, true);

        // Safely get the enumerators, including in the case where the root is not accessable
        if (root != null)
        {
          try
          {
            fileEnumerator = root.GetFileSystemInfos(pattern, SearchOption.TopDirectoryOnly).AsEnumerable<FileSystemInfo>().GetEnumerator();
          }
          catch (Exception ex)
          {
            errors.Add(ex);
            fileEnumerator = null;
          }

          try
          {
            directoryEnumerator = root.GetDirectories(pattern, SearchOption.TopDirectoryOnly).AsEnumerable<DirectoryInfo>().GetEnumerator();
          }
          catch (Exception ex)
          {
            errors.Add(ex);
            directoryEnumerator = null;
          }
        }
      }
    }

    public IEnumerator<FileSystemInfo> GetEnumerator()
    {
      return new Enumerator(root, pattern, searchOption, errors, depth);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }
}
