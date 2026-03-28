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
    class StatusReport
    {
        public static void Generate(string baseDir)
        {
            string extractedDir = Path.Combine(baseDir, "scripts", "extracted_texts");
            string ecoJsonPath = Path.Combine(baseDir, "eco.json");
            string reportPath = Path.Combine(baseDir, "trails_status_report.csv");

            var jsonContent = File.ReadAllText(ecoJsonPath);
            var ecoData = JsonConvert.DeserializeObject(jsonContent) as JObject;
            var trails = ecoData["eco_trails"] as JArray;

            var enrichmentMap = new Dictionary<int, string>(); // ID -> Confidence

            var csvFiles = Directory.GetFiles(extractedDir, "*.txt");
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null
            };

            foreach (var file in csvFiles)
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

                            if (int.TryParse(dict["id"]?.ToString(), out int id))
                            {
                                string conf = dict["confidence"]?.ToString()?.Trim().ToLower() ?? "unknown";
                                // Keep the highest confidence if multiple found
                                if (!enrichmentMap.ContainsKey(id) || conf == "high")
                                {
                                    enrichmentMap[id] = conf;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            using (var writer = new StreamWriter(reportPath))
            using (var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csvWriter.WriteField("ID");
                csvWriter.WriteField("Name");
                csvWriter.WriteField("CurrentDescLength");
                csvWriter.WriteField("ConfidenceInFiles");
                csvWriter.WriteField("Status");
                csvWriter.NextRecord();

                foreach (var trail in trails)
                {
                    int id = trail["id"].Value<int>();
                    string name = trail["name"]?.ToString();
                    string desc = trail["description"]?.ToString() ?? "";
                    int descLen = desc.Length;

                    string confidence = enrichmentMap.ContainsKey(id) ? enrichmentMap[id] : "none";
                    string status = "Needs Attention";

                    if (confidence == "high") status = "Ready to Enrich";
                    else if (descLen > 300) status = "Already Good";
                    else if (confidence == "medium") status = "Review Needed (Medium Conf)";
                    else if (confidence == "low") status = "Poor Data (Low Conf)";
                    else status = "No Data Found";

                    csvWriter.WriteField(id);
                    csvWriter.WriteField(name);
                    csvWriter.WriteField(descLen);
                    csvWriter.WriteField(confidence);
                    csvWriter.WriteField(status);
                    csvWriter.NextRecord();
                }
            }

            Console.WriteLine($"Status report generated: {reportPath}");
        }
    }
}
