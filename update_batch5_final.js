const fs = require('fs');

const updates = {
  482: "Маршрутът започва от кметството на с. Мърчаево и води до местността \"Церова поляна\".",
  510: "Маршрутът \"Климент\" е с дължина 18 км и е изграден от младежите на с. Климент.",
  513: "Маршрутът започва от центъра на Тетевен, минава през квартал \"Козница\" и достига до Скока водопад.",
  514: "Маршрутът се състои от две части – по ждрелото на река Ерма и по ждрелото на река Голяма Огостица.",
  516: "Маршрутът има дължина 13,5 км и е разположен между селата Иванча и Долец по поречието на река Голяма река.",
  518: "Маршрутът започва от църквата в с. Малко Градище и по пътя има три водопада.",
  519: "Маршрутът започва от края на град Чипровци, преминава през дървени мостчета и минава през дивата природа.",
  521: "Маршрутът около Мадарския конник предлага разходка из Националния историко-археологически резерват."
};

for (let file of ['ecoupdated.json', 'eco.json']) {
  const data = JSON.parse(fs.readFileSync(file, 'utf8'));
  let updated = 0;
  for (const id in updates) {
    const rec = data.eco_trails.find(x => x.id === parseInt(id));
    if (rec && rec.description.startsWith("Екопътека")) {
      // Find first period and continue
      const firstPeriodIdx = rec.description.indexOf('. ');
      const restOfDesc = firstPeriodIdx > -1 ? rec.description.substring(firstPeriodIdx + 1) : '';
      rec.description = updates[id] + ' ' + restOfDesc.trim();
      updated++;
    }
  }
  fs.writeFileSync(file, JSON.stringify(data, null, 2));
  console.log(file + ': ' + updated + ' updated');
}
