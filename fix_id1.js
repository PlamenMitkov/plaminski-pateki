const fs = require('fs');

// Update both files
['ecoupdated.json', 'eco.json'].forEach(file => {
  const data = JSON.parse(fs.readFileSync(file, 'utf8'));
  const record = data.eco_trails.find(x => x.id === 1);
  
  if (record) {
    // Fix 1: Correct email for Видин region (match region, not Plovdiv)
    record.contact_info.email = "info@vidinregion.com";
    
    // Fix 2: Add actual length in km (replacing placeholder "не е посочена")
    record.trail_details.length_km = "6.5";
    
    // Fix 3: Add short_summary field
    record.short_summary = "Екопътека през вековни гори до защитена местност Връшка чука със редкото растение Ерантис Булгарикум.";
  }
  
  fs.writeFileSync(file, JSON.stringify(data, null, 2));
  console.log(file + ': ID 1 fixed - email, length_km, short_summary');
});
