using System.Diagnostics;
using ObsidianDB;
using RagSharp;

namespace VulcanAi
{
    internal class Program
    {
        public static HyperVectorDB.HyperVectorDB? vectorDB;
        public static ObsidianDB.ObsidianDB? obsidianDB;
        public static ILLMProvider llm = new RagSharp.LMStudio();

        public static List<Tuple<string, string>> ConversationHistory = new(); // Prompt, Response

        static string? documentPath = null;
        static bool skippingBlock = false;
        private static string? CustomPreprocessor(string line, string? path, int? lineNumber)
        {
            if (string.IsNullOrWhiteSpace(line)) { return null; }

            if (path != null && path.ToUpperInvariant().EndsWith(".MD"))
            {
                if (line.Contains("---"))// Skip YAML frontmatter
                {
                    skippingBlock = false;
                    return null;
                }
                else if (line.Contains("```"))// Skip code blocks
                {
                    skippingBlock = !skippingBlock;
                    return null;
                }
                else
                {
                    if (line.EndsWith("aliases: ") ||
                        line.Contains("date created:") ||
                        line.Contains("date modified:") ||
                        (line.EndsWith(":") && !line.StartsWith("#"))
                    ) { return null; }
                }

                if (line.Contains("%%")) { return null; }//Skip annotation lines

                if (line.Trim().StartsWith("[[") && line.Trim().EndsWith("]]")) { return null; }//Skip index links

                if (skippingBlock) { return null; }

            }

            return line.Trim();
        }

        private static string? CustomPostprocessor(string line, string? path, int? lineNumber)
        {
            if (path == null && documentPath == null) { return $"TEXT|{line}"; }

            if (path != null && lineNumber != null){ return $"FILE|{path!}|{lineNumber}";}
            
            return $"FILE|{documentPath!}";
        }

        static void Main()
        {
            DiscordProvider discord = new DiscordProvider
            {
                //Token = File.ReadAllText(@"..\..\discord-token.secret")
            };

            //discord.Setup();

            obsidianDB = new()
            {
                VaultPath = @"C:\Users\owner\OneDrive\Apps\remotely-save\Vault"
            };
            Console.WriteLine("Scanning Vault");
            obsidianDB.ScanNotes();
            Console.WriteLine("Scanning Complete");

            Console.WriteLine("Vectorizing");
            vectorDB = new HyperVectorDB.HyperVectorDB(new HyperVectorDB.Embedder.LmStudio(), "TestDatabase", 32);

            try
            {
                vectorDB.Load();
            }
            catch
            {
                if(Directory.Exists(@".\TestDatabase")) System.IO.Directory.Delete(@".\TestDatabase");
                double progress = 0;
                double noteCount = obsidianDB.GetNotes().Count();
                double noteIndex = 0;
                var sw = new Stopwatch(); sw.Start();
                foreach (Note note in obsidianDB.GetNotes())
                {
                    
                    Console.WriteLine(note.Path);
                    documentPath = note.Path;
                    string body = note.GetBody();
                    body = ObsidianDB.Utilities.RemoveBlock(body, "```", "```");
                    string[] lines = body.Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (string line in lines)
                    {
                        if (ObsidianDB.Utilities.ContainsPlaintext(line))
                        {
                            //Console.WriteLine($"VECTORIZING: {line}");
                            vectorDB.IndexDocument(line, CustomPreprocessor, CustomPostprocessor);
                            Console.Write(":");
                        }
                        else{Console.Write(".");}
                    }
                    Console.WriteLine();
                    Console.Clear();
                    documentPath = null;
                    vectorDB.Save();

                    
                    double elapsedSeconds = ((double)sw.ElapsedMilliseconds) / 1000.0;
                    //Console.WriteLine($"{elapsedSeconds} seconds");
                    noteIndex += 1;
                    progress = noteIndex / noteCount;
                    double etaSeconds = 10 * elapsedSeconds / progress;
                    TimeSpan eta = new(0,0, (int)Math.Round(etaSeconds));
                    Console.WriteLine($"Progress: {Math.Round(progress*100)/100}%\t ETA: {eta:hh\\:mm\\:ss}");
                }
                sw.Stop();
            }

            Console.WriteLine("Done preparing");

            while (true)
            {
                Console.Write("> ");
                var prompt = Console.ReadLine();
                if (prompt == "exit") break;
                if (prompt is null) break;
                if (prompt is "") continue;
                var sw = new Stopwatch(); sw.Start();
                string context = GetContext(prompt, 8192);
                sw.Stop();
                Console.WriteLine("RAG processed in " + sw.ElapsedMilliseconds + "ms");
                string response = llm.Prompt($"CONTEXT:\n{context}\n========\nPROMPT:\n{prompt}");
                Console.WriteLine($"RESPONSE:\n{response}");
                StoreConversation(prompt, response);
            }

            Console.WriteLine("Done, press enter to exit");
            Console.ReadLine();

        }

        public static string GetContext(string? prompt = null, int maxTokens = 1024)
        {
            string result = "";

            if (prompt != null && vectorDB != null)
            {
                result += "THESE DOCUMENTS MIGHT BE RELEVANT\n";
                var searchResults = vectorDB.QueryCosineSimilarity(prompt, 100);
                for (var i = 0; i < searchResults.Documents.Count; i++)
                {
                    if (CountTokens(result) > maxTokens * 0.75) { break; }

                    string[] dbRecord = searchResults.Documents[i].DocumentString.Split('|');
                    string content = "";

                    if(dbRecord[0] == "TEXT"){ content = dbRecord[1]; }
                    if(dbRecord[0] == "FILE"){ content = Utilities.RemoveBlock( System.IO.File.ReadAllText(dbRecord[1]), "---", "---"); }

                    result += $"\n--------\n{content}";
                }
            }

            if (ConversationHistory.Count > 0)
            {
                result += "\n--------\nTHE CONVERSATION SO FAR:\n";
                int j = 0;
                while (CountTokens(result) < maxTokens && j < ConversationHistory.Count)
                {
                    Tuple<string, string> PromptResponse = ConversationHistory[j];
                    result += $"USER SAID:\n{PromptResponse.Item1}\nASSISTANT SAID:\n{PromptResponse.Item2}\n";
                    j++;
                }
            }

            Console.WriteLine(result);

            return result;
        }

        public static void StoreConversation(string prompt, string response)
        {
            ConversationHistory.Insert(0, new(prompt, response));
        }

        private static int CountTokens(string text)
        {
            string cleanText = string.Join(' ', text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            string[] tokens = cleanText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return tokens.Length;
        }
    }
}