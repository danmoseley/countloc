using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

class Program
{
    static async Task Main()
    {
        string repoFilePath = "c:\\loc\\repos.txt";
        string destinationFolder = "d:\\locrepos";
        string command = "c:\\t\\cloc-2.02.exe";
        string outputFile = "c:\\loc\\output.txt";

        if (!File.Exists(repoFilePath))
        {
            Console.WriteLine("The specified repo file does not exist.");
            return;
        }

        File.Delete(outputFile);

        string[] repoNames = await File.ReadAllLinesAsync(repoFilePath);
        var clonedRepos = new List<string>();

        // First loop: Clone all repositories
        foreach (string repoName in repoNames)
        {
            if (string.IsNullOrWhiteSpace(repoName) || repoName.StartsWith("#")) continue;

            string[] parts = SplitOnFirst(repoName, new char[] { '-', '/' });
            if (parts.Length != 2)
            {
                Console.WriteLine($"Invalid repo format: {repoName}");
                continue;
            }

            string owner = parts[0];
            string repo = parts[1];
            string repoPath = Path.Combine(destinationFolder, repo);

            // Attempt to clone the repository
            string result = "";

            if (!Directory.Exists(repoPath))
            {
                result = await RunCommandAsync("git", "C:\\Program Files\\Git\\cmd", $"clone --depth 1 https://github.com/{owner}/{repo}.git {repoPath}", false);
                if (result == null)
                {
                    result = await RunCommandAsync("git", "C:\\Program Files\\Git\\cmd", $"clone --depth 1 https://dev.azure.com/dnceng/internal/_git/{repoName} {repoPath}", true);
                }
            }

            if (result != null)
            {
                clonedRepos.Add(repoPath);
            }
        }

        // Second loop: Run the command on each cloned repository in parallel
        var tasks = clonedRepos.Select(async repoPath =>
        {
            string repoName = Path.GetFileName(repoPath);

            // Run the specified command in the cloned repository
            string output = await RunCommandAsync(command, repoPath, $"--timeout 100 {repoPath}", true);

            if (output == null)
            {
                WriteErrorInRed($"Error running cloc on {repoName}");
                return null;
            }

            // Extract the rightmost number on the row containing the text "SUM"
            string? sumLine = output.Split('\n').FirstOrDefault(line => line.Contains("SUM"));
            if (sumLine != null)
            {
                string rightMostNumber = sumLine.Split(' ').Last();
                return $"{repoName} {rightMostNumber}";
            }

            return null;
        }).ToList();

        var results = await Task.WhenAll(tasks);

        // Write results to the output file in the original order
        foreach (var result in results)
        {
            if (result != null)
            {
                Console.WriteLine(result);
                File.AppendAllText(outputFile, result + Environment.NewLine);
            }
        }

        Console.WriteLine($"Output in {outputFile}");
    }

    static void WriteErrorInRed(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    static string[] SplitOnFirst(string input, char[] delimiters)
    {
        int index = input.IndexOfAny(delimiters);
        if (index == -1)
        {
            return new string[] { input };
        }
        return new string[] { input.Substring(0, index), input.Substring(index + 1) };
    }

    static async Task<string> RunCommandAsync(string command, string workingDirectory, string arguments, bool errorOutput)
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        Console.WriteLine($"{command} {arguments}");

        using (Process process = Process.Start(processStartInfo))
        {
            using (StreamReader outputReader = process.StandardOutput)
            using (StreamReader errorReader = process.StandardError)
            {
                string output = await outputReader.ReadToEndAsync();
                string error = await errorReader.ReadToEndAsync();
                if (!string.IsNullOrEmpty(error))
                {
                    if (errorOutput) Console.Error.WriteLine(error);

                    return null;
                }
                return output;
            }
        }
    }
}