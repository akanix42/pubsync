using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using PubSync.Extensions;

namespace PubSync
{
    class Program
    {
        static string RobocopyArguments;
        static bool _showCommands = false;
        private static bool _syncFiles = true;
        private static bool _noDelete = false;
        private static string _xmlFilename = "pubsync.xml";

        static readonly ProcessStartInfo RobocopyInfo = new ProcessStartInfo
        {
            FileName = "robocopy.exe",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };

        static void Main(string[] argsArray)
        {
            if (argsArray.Length == 0)
            {
                Console.WriteLine("Publishing profile not specified!");
                Environment.ExitCode = 1;
                return;
            }
            var args = new Args(argsArray);
            RobocopyArguments = "{0} {1}\\{0} /NJH /NP /NDL /S ";
            if (args.HasArg("--noDelete"))
                _noDelete = true;
            else
                RobocopyArguments += " /MIR ";

            _showCommands = args.HasArg("--showCommands");
            _syncFiles = !args.HasArg("--noSync");
            _xmlFilename = args.GetArg("--xml") ?? _xmlFilename;

            RobocopyArguments += " {2} {3} {4}";

            var profileName = args.UnprefixedArgs[0];
            var syncSucceeded = args.HasArg("--file")
                                     ? SyncFile(profileName, LoadAndValidateXml(), args.GetArg("--file"))
                                     : SyncFolders(profileName, LoadAndValidateXml());

            Console.WriteLine(syncSucceeded ? "Sync finished." : "Sync failed.");
            if (Environment.ExitCode == 0 && !syncSucceeded)
                Environment.ExitCode = 1;
        }

        static void GetFilesInDir(int basePathLength, DirectoryInfo dir, ExclusionRules exclusionRules,  Dictionary<string, FileInfo> filesDictionary, string location)
        {
            foreach (var file in dir.GetFiles().Where(f => !exclusionRules.IsMatch(f.FullName, ExclusionRuleTypes.File, location)))
                filesDictionary.Add(file.FullName.Substring(basePathLength + 1).ToLower(), file);
            foreach (var subDir in dir.GetDirectories().Where(subDir => !exclusionRules.IsMatch(subDir.FullName, ExclusionRuleTypes.Folder, location)))
                GetFilesInDir(basePathLength, subDir, exclusionRules, filesDictionary, location);
        }

        static XmlSchemaSet LoadSchema()
        {
            var schemas = new XmlSchemaSet();
            var pubsyncXsdFile = new FileInfo(Path.Combine(new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName, "pubsync.xsd"));
            if (!pubsyncXsdFile.Exists)
            {
                Console.WriteLine("Unable to locate pubsync.xsd!\r\nPath: {0}", pubsyncXsdFile.FullName);
                return null;
            }
            bool errors = false;

            var xmlSchema = XmlSchema.Read(XmlReader.Create(pubsyncXsdFile.FullName), (o, error) =>
            {
                Console.WriteLine(error.Message);
                errors = true;
            });
            if (errors)
            {
                Console.WriteLine("Error parsing pubsync.xsd!\r\nPath: {0}", pubsyncXsdFile.FullName);
                return null;
            }
            schemas.Add(xmlSchema);

            return schemas;
        }

        static XDocument LoadAndValidateXml()
        {
            bool errors = false;
            var pubsyncXmlFile = new FileInfo(_xmlFilename);
            if (!pubsyncXmlFile.Exists)
            {
                Console.WriteLine("Unable to locate pubsync.xml!\r\nPath: {0}", pubsyncXmlFile.FullName);
                return null;
            }

            var schemas = LoadSchema();
            if (schemas == null)
                return null;

            var xdoc = XDocument.Load(_xmlFilename);

            xdoc.Validate(schemas, (o, error) =>
            {
                Console.WriteLine("{0}", error.Message);
                errors = true;
            });

            if (errors)
            {
                Console.WriteLine("Pubsync.xml does not adhere to the schema!\r\nPath: {0}", pubsyncXmlFile.FullName);
                return null;
            }
            return xdoc;
        }

        static void PrintSeparator()
        {
            Console.WriteLine("==============================================================================\r\n");

        }

        static bool SyncFile(string profileName, XDocument xdoc, string filename)
        {
            try
            {
                var sourceFile = new FileInfo(filename);
                if (!sourceFile.Exists)
                {
                    Console.WriteLine("Source file not found!\r\nFile: {0}", sourceFile.FullName);
                    return false;
                }

                if (xdoc == null)
                {
                    Console.WriteLine("XML Document is empty!");
                    return false;
                }

                var root = xdoc.Root;
                var ns = "{" + root.Name.Namespace + "}";
                var profile = root.Element(ns + "Profiles").Elements(ns + "Profile").SingleOrDefault(d => d.Name == ns + "Profile" && d.Attribute("Name").Value == profileName);
                if (profile == null)
                {
                    Console.WriteLine("Profile '{0}' does not exist!", profileName);
                    return false;
                }
                var publishingPath = profile.Attribute("PublishingPath").Value;
                Console.WriteLine("Publishing path: {0}", publishingPath);
                var projectDir = new DirectoryInfo(Environment.CurrentDirectory);
                if (!sourceFile.FullName.ToLower().Contains(projectDir.FullName.ToLower()))
                {
                    Console.WriteLine("Source file is not a descendant of base directory!\r\nBase: {0}\r\nFile: {1}\r\n", projectDir.FullName, sourceFile.FullName);
                    return false;
                }
                var relativePath = sourceFile.FullName.ToLower().Replace(projectDir.FullName.ToLower(), "").Trim(new[] { '\\' });
                Console.WriteLine("File: {0}", relativePath);
                var destFile = new FileInfo(Path.Combine(publishingPath, relativePath));
                if (destFile.Exists
                    && sourceFile.Length == destFile.Length
                    && sourceFile.LastWriteTime == destFile.LastWriteTime)
                {
                    Console.WriteLine("Result: Source is identical to destination.");
                    return true;
                }

                sourceFile.CopyTo(destFile.FullName, true);
                Console.WriteLine("Result: File updated!");
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: {0}", e);
                return false;
            }
        }

        static bool SyncFolders(string profileName, XDocument xdoc)
        {
            if (xdoc == null)
                return false;

            var root = xdoc.Root;
            var ns = "{" + root.Name.Namespace + "}";
            var profile = root.Element(ns + "Profiles").Elements(ns + "Profile").SingleOrDefault(d => d.Name == ns + "Profile" && d.Attribute("Name").Value == profileName);
            if (profile == null)
            {
                Console.WriteLine("Profile '{0}' does not exist!", profileName);
                return false;
            }
            var publishingPath = profile.Attribute("PublishingPath").Value;

            // Copy the files!
            var wasSuccessful = true;
            foreach (var folder in root.Element(ns + "Folders").Elements(ns + "Folder"))
            {
                var copyMethod = folder.Attribute("CopyMethod");
                if (copyMethod != null && copyMethod.Value.ToLower() == "pubsync")
                    wasSuccessful = wasSuccessful && SyncUsingPubsync(publishingPath, ns, folder, profile);
                else
                    wasSuccessful = wasSuccessful && SyncUsingRobocopy(publishingPath, ns, folder);
            }

            return wasSuccessful;
        }

        static string ApplyReplacementsToFileName(string filename, List<Replacement> replacements)
        {
            return replacements.Aggregate(filename, (current, replacement) => replacement.Replace(current));
        }

        static List<Replacement> GetReplacementRules(string xmlNamespace, XElement element)
        {
            return element.Elements(xmlNamespace + "Replace")
                        .Select(
                            d => new Replacement()
                            {
                                Expression =
                                    new Regex(d.Attribute("Expression").Value, RegexOptions.Compiled & RegexOptions.IgnoreCase),
                                ReplacementText = d.Attribute("Replacement").Value
                            }
                        )
                        .ToList();
        }

        static List<ExclusionRule> GetExclusionRules(string xmlNamespace, XElement element)
        {
            return element.Elements(xmlNamespace + "Exclude")
                .Select(
                    d => new ExclusionRule(d.Attribute("Expression").Value)
                         {
                             Location = d.GetAttributeValueOrNull("Location"),
                             Invert = bool.Parse(d.GetAttributeValueOrNull("Invert") ?? "false"),
                             Type = (ExclusionRuleTypes) Enum.Parse(typeof (ExclusionRuleTypes), d.Attribute("Type").Value)
                         }
                )
                .ToList();
        }

        static bool SyncUsingPubsync(string publishingPath, string xmlNamespace, XElement folder, XElement profile)
        {
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var folderPath = folder.Attribute("Path").Value;
                Console.WriteLine("{0}", folderPath);
                Console.WriteLine("------------------------------------------------------------------------------");

                //var filesToExclude = folder.Elements(xmlNamespace + "Exclude").Where(e => e.Attribute("Type").Value == "File")
                //   .Select(
                //             d =>
                //             new Regex(d.Attribute("Expression").Value, RegexOptions.Compiled & RegexOptions.IgnoreCase))
                //         .ToList();
                var exclusionRules = new ExclusionRules(GetExclusionRules(xmlNamespace, folder));
                exclusionRules.Rules.AddRange(GetExclusionRules(xmlNamespace, profile));

                //var foldersToExclude =
                //folder.Elements(xmlNamespace + "Exclude").Where(e => e.Attribute("Type").Value == "Folder")
                //      .Select(
                //          d =>
                //          new Regex(d.Attribute("Expression").Value, RegexOptions.Compiled & RegexOptions.IgnoreCase))
                //      .ToList();

                var replacementRules = GetReplacementRules(xmlNamespace, folder);
                replacementRules.AddRange(GetReplacementRules(xmlNamespace, profile));

                var sourceDir = new DirectoryInfo(folderPath);
                var sourceFilesDictionary = new Dictionary<string, FileInfo>();
                if (!sourceDir.Exists)
                {
                    Console.WriteLine("Folder '{0}' does not exist on the source.", folderPath);
                    PrintSeparator();
                    return true;
                }
                GetFilesInDir(sourceDir.FullName.Length, sourceDir, exclusionRules, sourceFilesDictionary, "Source");

                var destDir = new DirectoryInfo(Path.Combine(publishingPath, folderPath));
                var destFilesDictionary = new Dictionary<string, FileInfo>();
                if (!destDir.Exists)
                    destDir.Create();

                GetFilesInDir(destDir.FullName.Length, destDir, exclusionRules, destFilesDictionary, "Destination");

                int changedFilesCount = 0;
                int deletedFilesCount = 0;
                int newFilesCount = 0;
                int skippedFilesCount = 0;
                int errorCount = 0;

                var keyMap = sourceFilesDictionary.Keys
                    .ToDictionary(x => x, x => ApplyReplacementsToFileName(x, replacementRules));


                // Check source files for new / changed
                foreach (var sourceFileEntry in sourceFilesDictionary)
                {
                    try
                    {
                        FileInfo destFile;
                        var destKey = keyMap[sourceFileEntry.Key];
                        if (destFilesDictionary.TryGetValue(destKey, out destFile))
                        {
                            if (sourceFileEntry.Value.Length != destFile.Length ||
                                sourceFileEntry.Value.LastWriteTime != destFile.LastWriteTime)
                            {
                                // Console.WriteLine("C: {0}", sourceFileEntry.Value.Name);
                                if (_syncFiles)
                                    sourceFileEntry.Value.CopyTo(Path.Combine(destDir.FullName, destKey), true);
                                changedFilesCount++;
                            }
                            else
                                skippedFilesCount++;
                        }
                        else
                        {
                            // Console.WriteLine("N: {0}", sourceFileEntry.Value.Name);
                            if (_syncFiles)
                            {
                                destFile = new FileInfo(Path.Combine(destDir.FullName, destKey));
                                if (!destFile.Directory.Exists)
                                    destFile.Directory.Create();
                                sourceFileEntry.Value.CopyTo(destFile.FullName, true);
                            }
                            newFilesCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        errorCount++;
                    }
                }

                // Check for orphaned files to delete
                if (_syncFiles && !_noDelete)
                    foreach (var fileKey in destFilesDictionary.Keys.Except(keyMap.Values))
                    {
                        try
                        {
                            destFilesDictionary[fileKey].Delete();
                            deletedFilesCount++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                            errorCount++;
                        }
                    }

                stopwatch.Stop();
                Console.WriteLine("Skipped: {0}\r\nNew: {1}\r\nChanged: {2}\r\nDeleted: {3}\r\nErrors: {4}", skippedFilesCount, newFilesCount, changedFilesCount, deletedFilesCount, errorCount);
                Console.WriteLine("Time: {0}", stopwatch.Elapsed);
                PrintSeparator();
                return errorCount==0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }

        static bool SyncUsingRobocopy(string publishingPath, string xmlNamespace, XElement folder)
        {
            var stopWatch = new Stopwatch();
            var foldersToExclude = folder.Elements(xmlNamespace + "Exclude").Where(e => e.Attribute("Type").Value == "Folder").Select(d => d.Attribute("Expression").Value).ToList();
            var filesToExclude = folder.Elements(xmlNamespace + "Exclude").Where(e => e.Attribute("Type").Value == "File").Select(d => d.Attribute("Expression").Value).ToList();
            var robocopy = new Process { StartInfo = RobocopyInfo };
            var folderPath = folder.Attribute("Path").Value;
            var levelsToSync = folder.Attribute("LevelsToSync");

            robocopy.StartInfo.Arguments = String.Format(RobocopyArguments,
                                                         new object[]
                                                                 {
                                                                     folderPath,
                                                                     publishingPath,
                                                                     foldersToExclude.Any() ? "/XD " + String.Join(" ", foldersToExclude) : "",
                                                                     filesToExclude.Any() ? "/XF " + String.Join(" ", filesToExclude) : "",
                                                                     levelsToSync != null ? "/LEV:" + levelsToSync.Value : ""
                                                                 });
            if (_showCommands)
                Console.WriteLine("robocopy {0}", robocopy.StartInfo.Arguments);
            Console.WriteLine("{0}", folderPath);
            var robocopyOutput = new StringBuilder();
            robocopyOutput.AppendLine("------------------------------------------------------------------------------");
            if (_syncFiles)
            {
                robocopy.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        robocopyOutput.AppendLine(e.Data);
                };
                robocopy.EnableRaisingEvents = true;
                stopWatch.Start();
                robocopy.Start();
                robocopy.BeginOutputReadLine();
                robocopy.WaitForExit();
            }
            stopWatch.Stop();
            robocopyOutput.AppendLine(String.Format("Time: {0}", stopWatch.Elapsed));
            robocopyOutput.AppendLine("==============================================================================");

            var outputString = Regex.Replace(robocopyOutput.ToString(), @"\r\n\r\n---+\r\n", "");
            outputString = Regex.Replace(outputString, @"\r\n\s+Times :.*?Ended :.*?\r\n", "\r\n", RegexOptions.Singleline);

            Console.WriteLine(outputString);
            
            return robocopy.ExitCode == 0;
        }

    }
}
