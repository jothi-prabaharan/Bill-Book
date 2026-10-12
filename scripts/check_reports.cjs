const fs = require('fs');
const r = JSON.parse(fs.readFileSync('backend/Api/Reporting/Reporting.Repository/Configurations/reports.json', 'utf8'));
const menuSeedText = fs.readFileSync('backend/Api/Master/Master.Repository/SeedData/MenuSeed.cs', 'utf8');

const reportsInJson = new Map();
r.Reports.forEach(x => {
  const g = x.ReportGroup.trim();
  const n = x.ReportName.trim();
  if (!reportsInJson.has(n)) {
    reportsInJson.set(n, g);
  }
});

console.log('Total in reports.json:', reportsInJson.size);

for (const [n, g] of reportsInJson.entries()) {
  const needle = `"${n.toLowerCase()}"`;
  if (!menuSeedText.toLowerCase().includes(needle)) {
    console.log('MISSING:', n, '| Group:', g);
  }
}
