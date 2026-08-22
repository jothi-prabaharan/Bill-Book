import os, glob, re

files = glob.glob(r'C:\Users\Praba\Source\repos\Bill-Book\frontend\libs\sales\sales-ui\src\lib\*-form\*.component.html')

def fix_labels(match):
    label_text = match.group(1)
    tag = match.group(2)
    id_name = match.group(3)
    rest = match.group(4)
    return f'<label for="{id_name}">{label_text}</label>\n      <{tag} id="{id_name}" formControlName="{id_name}"{rest}>'

for f in files:
    with open(f, 'r', encoding='utf-8') as file:
        content = file.read()
    
    pattern = re.compile(r'<label>([^<]+)</label>\s*<(input|textarea|select)[^>]*formControlName="([^"]+)"([^>]*)>')
    new_content = pattern.sub(fix_labels, content)
    
    with open(f, 'w', encoding='utf-8') as file:
        file.write(new_content)
print('Done!')
