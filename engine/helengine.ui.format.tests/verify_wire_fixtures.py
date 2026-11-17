from pathlib import Path
import struct
import xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
tests=root/'engine/helengine.ui.format.tests'
raw=bytes.fromhex((tests/'card-v1.hex').read_text().strip())
pos=0
def read(fmt):
    global pos
    values=struct.unpack_from('<'+fmt,raw,pos)
    pos+=struct.calcsize('<'+fmt)
    return values[0] if len(values)==1 else values
def string():
    global pos
    size=read('I');data=raw[pos:pos+size];pos+=size
    return data.decode('utf-8',errors='strict')
assert raw[:4]==b'HELE';pos=4
assert read('BBHHHI')==(1,1,0x5549,1,1,len(raw)-16)
assert read('H')==1 and string()=='gap' and read('f')==16
assert read('H')==1 and string()=='card' and read('I')==1
assert read('ii')==(1,-1) and string()=='ação 🖌'
assert read('5fBH')==(-1.25,2.5,0,230,230,1,1)
assert read('HHHB')==(1,2,1,9) and string()=='Surface'
assert read('HBf')==(4,1,16) and pos==len(raw)
assert bytes.fromhex((tests/'empty-v1.hex').read_text().strip())==b'HELE'+struct.pack('<BBHHHIHH',1,1,0x5549,1,1,4,0,0)
projects=[root/'engine/helengine.ui.format/helengine.ui.format.csproj',tests/'helengine.ui.format.tests.csproj',root/'tools/ui-format-export/ui-format-export.csproj']
for project in projects:
    doc=ET.parse(project)
    for reference in doc.findall('.//ProjectReference'):
        assert (project.parent/reference.attrib['Include']).resolve().is_file(),reference.attrib
print('PASS: 2 independent wire fixtures decoded field by field; 3 project XML files/references checked. C# and TS implementations NOT executed.')
