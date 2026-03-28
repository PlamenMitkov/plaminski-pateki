using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DataEnricher
{
    class Program
    {
        static void Main(string[] args)
        {
            string baseDir = @"c:\Users\35987\source\repos\EcoProject";
            
            Console.WriteLine("Generating comprehensive status report...");
            StatusReport.Generate(baseDir);

            string extractedDir = Path.Combine(baseDir, "scripts", "extracted_texts");
            string enrichmentDir = Path.Combine(baseDir, "scripts", "description-enrichment");
            string ecoJsonPath = Path.Combine(baseDir, "eco.json");
            string outputPath = Path.Combine(baseDir, "eco_enriched.json");

            Console.WriteLine("Loading eco.json...");
            var jsonContent = File.ReadAllText(ecoJsonPath);
            var ecoData = JsonConvert.DeserializeObject(jsonContent) as JObject;
            var trails = ecoData["eco_trails"] as JArray;

            var enrichmentMap = new Dictionary<int, TrailEnrichment>();

            Console.WriteLine("Scanning all data sources (CSVs and Extracted Docs)...");
            var allFiles = Directory.GetFiles(extractedDir, "*.txt").ToList();
            if (Directory.Exists(enrichmentDir))
            {
                allFiles.AddRange(Directory.GetFiles(enrichmentDir, "*.csv"));
            }

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null,
                PrepareHeaderForMatch = args => args.Header.ToLower()
            };

            foreach (var file in allFiles)
            {
                try
                {
                    using (var reader = new StreamReader(file))
                    using (var csv = new CsvReader(reader, config))
                    {
                        var records = csv.GetRecords<dynamic>().ToList();
                        foreach (var record in records)
                        {
                            var dict = (IDictionary<string, object>)record;
                            if (!dict.ContainsKey("id") || !dict.ContainsKey("confidence")) continue;

                            string idStr = dict["id"]?.ToString()?.Trim('\"', ' ');
                            string confidence = dict["confidence"]?.ToString()?.Trim('\"', ' ').ToLower();

                            if (int.TryParse(idStr, out int id) && confidence == "high")
                            {
                                enrichmentMap[id] = new TrailEnrichment
                                {
                                    Description = dict.ContainsKey("enriched_description_bg") ? dict["enriched_description_bg"]?.ToString() : null,
                                    ShortSummary = dict.ContainsKey("short_summary_bg") ? dict["short_summary_bg"]?.ToString() : null,
                                    Cautions = dict.ContainsKey("cautions") ? dict["cautions"]?.ToString() : null
                                };
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            Console.WriteLine($"Found {enrichmentMap.Count} high-confidence enrichments.");

            int updatedCount = 0;
            foreach (var trail in trails)
            {
                int id = trail["id"].Value<int>();
                if (enrichmentMap.ContainsKey(id))
                {
                    var enrichment = enrichmentMap[id];

                    if (!string.IsNullOrWhiteSpace(enrichment.Description))
                    {
                        trail["description"] = enrichment.Description;
                    }

                    if (!string.IsNullOrWhiteSpace(enrichment.ShortSummary))
                    {
                        trail["short_summary"] = enrichment.ShortSummary;
                    }

                    if (!string.IsNullOrWhiteSpace(enrichment.Cautions))
                    {
                        var cautions = enrichment.Cautions.Split('|', ',')
                                        .Select(c => c.Trim())
                                        .Where(c => !string.IsNullOrEmpty(c))
                                        .ToList();
                        
                        if (cautions.Any())
                        {
                            trail["safety_warnings"] = JArray.FromObject(cautions);
                        }
                    }

                    if (trail["metadata"] == null) trail["metadata"] = new JObject();
                    trail["metadata"]["ai_enriched"] = true;
                    trail["metadata"]["enrichment_date"] = DateTime.Now.ToString("yyyy-MM-dd");

                    updatedCount++;
                }
            }

            File.WriteAllText(outputPath, JsonConvert.SerializeObject(ecoData, Formatting.Indented));

            Console.WriteLine("------------------------------------------------");
            Console.WriteLine($"Success! Updated {updatedCount} trails.");
            Console.WriteLine($"Output saved to: {outputPath}");
            Console.WriteLine("------------------------------------------------");
        }
    }

    public class TrailEnrichment
    {
        public string Description { get; set; }
        public string ShortSummary { get; set; }
        public string Cautions { get; set; }
    }
}
