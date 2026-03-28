import os
import json
import csv
import glob

extracted_dir = r"c:\Users\35987\source\repos\EcoProject\scripts\extracted_texts"
eco_json_path = r"c:\Users\35987\source\repos\EcoProject\eco.json"
output_json_path = r"c:\Users\35987\source\repos\EcoProject\eco_updated.json"

def read_eco_json():
    with open(eco_json_path, 'r', encoding='utf-8') as f:
        return json.load(f)

def parse_csv_files():
    all_trails = {}
    total_found = 0
    high_confidence = 0
    
    files = glob.glob(os.path.join(extracted_dir, "*.txt"))
    for file_path in files:
        with open(file_path, 'r', encoding='utf-8') as f:
            reader = csv.reader(f)
            headers = None
            try:
                for row in reader:
                    if not row:
                        continue
                    if headers is None:
                        headers = row
                        continue
                    
                    if len(row) < 18:
                        continue
                        
                    trail_id = row[0].strip('"\n ')
                    if not trail_id.isdigit():
                        continue
                    
                    trail_id = int(trail_id)
                    confidence = row[17].strip().lower()
                    
                    total_found += 1
                    if confidence == 'high':
                        high_confidence += 1
                        all_trails[trail_id] = {
                            "description": row[8].strip(), # enriched_description_bg
                            "short_summary": row[9].strip(), # short_summary_bg
                            "attractions_str": row[10].strip(), # key_highlights
                            "terrain_diff": row[11].strip(), # terrain_and_difficulty
                            "suitable": row[12].strip(), # suitable_for
                            "season": row[13].strip(), # best_season
                            "cautions": row[14].strip(), # cautions
                            "nearby": row[15].strip(), # nearby_points_of_interest
                            "sources": row[16].strip(), # sources_urls
                        }
            except Exception as e:
                print(f"Error parsing {file_path}: {e}")
                
    print(f"Total trails found in CSVs: {total_found}")
    print(f"High confidence trails: {high_confidence}")
    return all_trails

def update_eco_json(eco_data, enriched_trails):
    updated_count = 0
    for trail in eco_data.get("eco_trails", []):
        t_id = trail.get("id")
        if t_id in enriched_trails:
            enriched = enriched_trails[t_id]
            if enriched["description"]:
                trail["description"] = enriched["description"]
            if enriched.get("short_summary"):
                trail["short_summary"] = enriched["short_summary"]
            
            # Additional fields or metadata enrichment
            if "metadata" not in trail:
                trail["metadata"] = {}
            trail["metadata"]["ai_enriched"] = True
            trail["metadata"]["ai_enrichment_confidence"] = "high"
            
            if enriched.get("cautions"):
                trail["safety_warnings"] = [c.strip() for c in enriched["cautions"].split(',')]
            
            updated_count += 1
            
    print(f"Updated {updated_count} trails in eco.json data structure.")
    return eco_data

if __name__ == "__main__":
    eco_data = read_eco_json()
    enriched_trails = parse_csv_files()
    updated_eco_data = update_eco_json(eco_data, enriched_trails)
    
    with open(output_json_path, 'w', encoding='utf-8') as f:
        json.dump(updated_eco_data, f, ensure_ascii=False, indent=2)
    print(f"Saved to {output_json_path}")
