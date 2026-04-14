const fs = require('fs');

const mismatched = [401, 421, 424, 459, 470, 475, 481];
const eco = JSON.parse(fs.readFileSync('eco.json', 'utf8'));
const ecoUpdated = JSON.parse(fs.readFileSync('ecoupdated.json', 'utf8'));

for (const id of mismatched) {
  const ecoRec = eco.eco_trails.find(x => x.id === id);
  const updatedRec = ecoUpdated.eco_trails.find(x => x.id === id);
  if (ecoRec && updatedRec) {
    updatedRec.description = ecoRec.description;
    console.log('Fixed ID ' + id);
  }
}

fs.writeFileSync('ecoupdated.json', JSON.stringify(ecoUpdated, null, 2));
console.log('Sync complete');
