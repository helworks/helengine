using System;
using System.Collections.Generic;
using System.IO;

namespace Nucleus {
    public static class DirectoryUtil {
        public static void RecursiveList(DirectoryInfo source, List<FileInfo> list) {
            foreach (FileInfo file in source.GetFiles()) {
                list.Add(file);
            }

            foreach (DirectoryInfo subDirectory in source.GetDirectories()) {
                RecursiveList(subDirectory, list);
            }
        }

        public static void RecursiveCopy(DirectoryInfo source, DirectoryInfo target) {
            // Ensure the target directory exists
            if (!target.Exists) {
                target.Create();
            }

            // Copy each file in the directory
            foreach (FileInfo file in source.GetFiles()) {
                string targetFilePath = Path.Combine(target.FullName, file.Name);
                file.CopyTo(targetFilePath, true); // true to overwrite existing files
            }

            // Copy each subdirectory
            foreach (DirectoryInfo subDirectory in source.GetDirectories()) {
                DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(subDirectory.Name);
                RecursiveCopy(subDirectory, nextTargetSubDir);
            }
        }

        public static void RecursiveCopy(DirectoryInfo source, DirectoryInfo target, Action<FileInfo, StreamReader, StreamWriter> callback, Func<FileInfo, bool> shouldCopy = null) {
            // Ensure the target directory exists
            if (!target.Exists) {
                target.Create();
            }

            // Copy each file in the directory
            foreach (FileInfo file in source.GetFiles()) {
                if (shouldCopy != null &&
                    !shouldCopy(file)) {
                    continue;
                }

                string targetFilePath = Path.Combine(target.FullName, file.Name);
                if (File.Exists(targetFilePath)) {
                    File.Delete(targetFilePath);
                }

                using (Stream str = file.OpenRead()) {
                    using (StreamReader reader = new StreamReader(str)) {
                        using (Stream newStr = File.OpenWrite(targetFilePath)) {
                            using (StreamWriter writer = new StreamWriter(newStr)) {
                                callback(file, reader, writer);

                                writer.Flush();
                            }
                        }
                    }
                }
            }

            // Copy each subdirectory
            foreach (DirectoryInfo subDirectory in source.GetDirectories()) {
                DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(subDirectory.Name);
                RecursiveCopy(subDirectory, nextTargetSubDir, callback);
            }
        }

        public static void GetFiles(DirectoryInfo baseDir, Func<FileInfo, bool> filter, List<FileInfo> allFiles) {
            FileInfo[] files = baseDir.GetFiles();

            if (filter == null) {
                allFiles.AddRange(files);
            } else {
                for (int i = 0; i < files.Length; i++) {
                    FileInfo file = files[i];
                    if (filter(file)) {
                        continue;
                    }

                    allFiles.Add(file);
                }
            }

            DirectoryInfo[] childDirs = baseDir.GetDirectories();
            for (int i = 0; i < childDirs.Length; i++) {
                DirectoryInfo dirInfo = childDirs[i];
                GetFiles(dirInfo, filter, allFiles);
            }
        }

        public static List<FileInfo> GetFiles(DirectoryInfo baseDir, Func<FileInfo, bool> filter) {
            List<FileInfo> allFiles = new List<FileInfo>();
            GetFiles(baseDir, filter, allFiles);
            return allFiles;
        }

        public static List<FileInfo> GetFiles(DirectoryInfo baseDir) {
            List<FileInfo> allFiles = new List<FileInfo>();
            GetFiles(baseDir, null, allFiles);
            return allFiles;
        }
    }
}
