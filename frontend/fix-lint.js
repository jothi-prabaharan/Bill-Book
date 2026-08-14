const fs = require('fs');
const glob = require('glob');

const files = glob.sync('libs/sales/sales-ui/src/lib/*-form/*.component.html');

for (const f of files) {
  let content = fs.readFileSync(f, 'utf-8');
  
  content = content.replace(/<label>([^<]+)<\/label>\s*<(input|textarea|select)([^>]*)formControlName="([^"]+)"([^>]*)>/g, 
    '<label for="$4">$1</label>\n      <$2 id="$4"$3formControlName="$4"$5>');

  fs.writeFileSync(f, content, 'utf-8');
}
console.log('Fixed linting errors in HTML files.');
