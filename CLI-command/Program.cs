using System.CommandLine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

class Program
{
    static async Task Main(string[] args)
    {
        var rspFileOption = new Option<FileInfo>(
            new[] { "--rsp-file", "-r" },
            "Path to the response file containing arguments for the bundle command.")
        {
            IsRequired = false
        };

        var outputOption = new Option<FileInfo>(
            new[] { "--output", "-o" },
            "File path and name for the output bundle file.")
        {
            IsRequired = false
        };

        var languageOption = new Option<string[]>(
            new[] { "--language", "-l" },
            description: "List of programming languages to include (comma-separated). Use 'all' for all languages.")
        {
            IsRequired = false
        };

        var noteOption = new Option<bool>(
            new[] { "--note", "-n" },
            "Include file origin as comments.")
        {
            IsRequired = false
        };

        var sortOption = new Option<string>(
            new[] { "--sort", "-s" },
            "Sorting order: 'name' (default) or 'type'.")
        {
            IsRequired = false
        };

        var removeEmptyLinesOption = new Option<bool>(
            new[] { "--remove-empty-lines", "-e" },
            "Remove empty lines from source files.")
        {
            IsRequired = false
        };

        var bundleCommand = new Command("bundle", "Bundle code files into a single file.");
        bundleCommand.AddOption(rspFileOption);
        bundleCommand.AddOption(outputOption);
        bundleCommand.AddOption(languageOption);
        bundleCommand.AddOption(noteOption);
        bundleCommand.AddOption(sortOption);
        bundleCommand.AddOption(removeEmptyLinesOption);

        bundleCommand.SetHandler(
            async (FileInfo rspFile, FileInfo output, string[] language, bool note, string sort, bool removeEmptyLines) =>
            {
                try
                {
                    // If rsp file is provided, read arguments from it
                    if (rspFile != null && rspFile.Exists)
                    {
                        var rspArgs = File.ReadAllLines(rspFile.FullName)
                            .Where(line => !string.IsNullOrWhiteSpace(line))
                            .Select(line => line.Trim().Split('='))
                            .ToDictionary(parts => parts[0], parts => parts[1]);

                        if (rspArgs.ContainsKey("output"))
                            output = new FileInfo(rspArgs["output"]);

                        if (rspArgs.ContainsKey("language"))
                            language = rspArgs["language"].Split(',');

                        if (rspArgs.ContainsKey("note"))
                            note = bool.Parse(rspArgs["note"]);

                        if (rspArgs.ContainsKey("sort"))
                            sort = rspArgs["sort"];

                        if (rspArgs.ContainsKey("removeEmptyLines"))
                            removeEmptyLines = bool.Parse(rspArgs["removeEmptyLines"]);
                    }

                    // Validate and process inputs
                    if (output == null || string.IsNullOrWhiteSpace(output.FullName))
                    {
                        Console.WriteLine("Error: Output file is required.");
                        return;
                    }

                    if (string.IsNullOrEmpty(sort))
                        sort = "name";

                    var currentDirectory = Directory.GetCurrentDirectory();
                    var restrictedFolders = new[] { "bin", "debug" };
                    if (restrictedFolders.Any(folder => currentDirectory.Contains(folder, StringComparison.OrdinalIgnoreCase)))
                    {
                        Console.WriteLine("Error: Cannot run this command in restricted folders.");
                        return;
                    }

                    if (!output.Directory.Exists)
                    {
                        Console.WriteLine("Error: The specified output directory does not exist.");
                        return;
                    }

                    var validLanguages = new[] { "csharp", "python", "javascript", "java", "html", "css", "jsx", "angular", "all" };
                    var languageExtensions = new Dictionary<string, string[]>
                    {
                        { "csharp", new[] { ".cs" } },
                        { "python", new[] { ".py" } },
                        { "javascript", new[] { ".js" } },
                        { "java", new[] { ".java" } },
                        { "html", new[] { ".html", ".htm" } },
                        { "css", new[] { ".css" } },
                        { "jsx", new[] { ".jsx" } },
                        { "angular", new[] { ".ts" } }
                    };

                    var extensionsToInclude = language[0].ToLower() == "all"
                        ? languageExtensions.Values.SelectMany(ext => ext).Distinct().ToArray()
                        : language
                            .Where(lang => validLanguages.Contains(lang.ToLower()))
                            .SelectMany(lang => languageExtensions[lang.ToLower()])
                            .Distinct()
                            .ToArray();

                    var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.*", SearchOption.AllDirectories)
                        .Where(file => extensionsToInclude.Contains(Path.GetExtension(file).ToLower()))
                        .ToList();

                    if (!files.Any())
                    {
                        Console.WriteLine("No files matching the specified languages were found.");
                        return;
                    }

                    files = sort == "type"
                        ? files.OrderBy(file => Path.GetExtension(file)).ThenBy(Path.GetFileName).ToList()
                        : files.OrderBy(Path.GetFileName).ToList();

                    using var writer = new StreamWriter(output.FullName);
                    foreach (var file in files)
                    {
                        if (note)
                        {
                            writer.WriteLine($"// Origin: {file}");
                        }

                        var lines = File.ReadAllLines(file);
                        if (removeEmptyLines)
                        {
                            lines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
                        }

                        foreach (var line in lines)
                        {
                            writer.WriteLine(line);
                        }

                        writer.WriteLine("\n####");
                    }

                    Console.WriteLine($"Bundle created at: {output.FullName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            },
            rspFileOption,
            outputOption,
            languageOption,
            noteOption,
            sortOption,
            removeEmptyLinesOption
        );

        var createRspCommand = new Command("create-rsp", "Create a response file with arguments for the bundle command.");
        createRspCommand.SetHandler(() =>
        {
            try
            {
                Console.Write("Enter output file path: ");
                string outputPath = Console.ReadLine()!;

                Console.Write("Enter programming languages (comma-separated, or 'all'): ");
                string languages = Console.ReadLine()!;

                Console.Write("Include file origin as comments? (yes/no): ");
                string noteInput = Console.ReadLine()!;
                bool note = noteInput.Equals("yes", StringComparison.OrdinalIgnoreCase);

                Console.Write("Enter sort order ('name' or 'type'): ");
                string sort = Console.ReadLine()!;

                Console.Write("Remove empty lines? (yes/no): ");
                string removeEmptyLinesInput = Console.ReadLine()!;
                bool removeEmptyLines = removeEmptyLinesInput.Equals("yes", StringComparison.OrdinalIgnoreCase);

                Console.Write("Enter response file name (without extension): ");
                string responseFileName = Console.ReadLine()!;

                var rspContent = $"output={outputPath}\n" +
                                 $"language={languages}\n" +
                                 $"note={note.ToString().ToLower()}\n" +
                                 $"sort={sort}\n" +
                                 $"removeEmptyLines={removeEmptyLines.ToString().ToLower()}";

                var rspFilePath = $"{responseFileName}.rsp";
                File.WriteAllText(rspFilePath, rspContent);
                Console.WriteLine($"Response file created: {rspFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        });

        var rootCommand = new RootCommand("CLI tool for bundling files and creating response files.");
        rootCommand.AddCommand(bundleCommand);
        rootCommand.AddCommand(createRspCommand);

        await rootCommand.InvokeAsync(args);
    }
}
