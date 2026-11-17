using System.Diagnostics;
using System.IO;
using UnityEditor;

namespace TrashTD.Editor
{
    /// <summary>
    /// Turns on the repository's git hooks (<c>.githooks/</c>, which run <c>Tools/check_project.py</c> before every
    /// commit and push) the first time the project is opened in a clone, so nobody has to remember to set them up.
    /// It only sets the local <c>core.hooksPath</c> setting of this clone, and only when it is not already set.
    /// <c>python Tools/setup_hooks.py</c> does the same by hand.
    /// </summary>
    [InitializeOnLoad]
    public static class GitHooksInstaller
    {
        private const string HooksFolder = ".githooks";

        static GitHooksInstaller()
        {
            // Run once the editor has finished loading so a slow git call never blocks the import.
            EditorApplication.delayCall += EnsureHooksEnabled;
        }

        private static void EnsureHooksEnabled()
        {
            try
            {
                // The Unity project is the TRASH-TD folder inside the repository root.
                string repositoryRoot = Directory.GetParent(Directory.GetCurrentDirectory())?.FullName;
                if (repositoryRoot == null || !Directory.Exists(Path.Combine(repositoryRoot, HooksFolder))) return;

                string current = RunGit(repositoryRoot, "config --get core.hooksPath");
                if (current == HooksFolder) return;
                if (!string.IsNullOrEmpty(current))
                {
                    UnityEngine.Debug.LogWarning("Git hooks: core.hooksPath is '" + current + "', not '" + HooksFolder +
                                                 "', so the repository checks will not run before commits. Run: git config core.hooksPath " + HooksFolder);
                    return;
                }

                RunGit(repositoryRoot, "config core.hooksPath " + HooksFolder);
                UnityEngine.Debug.Log("Git hooks turned on: Tools/check_project.py now runs before every commit and push.");
            }
            catch (System.Exception)
            {
                // Git is not installed or this is not a repository: nothing to set up.
            }
        }

        private static string RunGit(string workingDirectory, string arguments)
        {
            var info = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);
                return output;
            }
        }
    }
}
