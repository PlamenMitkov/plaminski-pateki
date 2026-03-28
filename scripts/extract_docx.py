import zipfile
import xml.etree.ElementTree as ET
import os

def extract_text_from_docx(docx_path):
    namespaces = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
    text = []
    try:
        with zipfile.ZipFile(docx_path) as docx:
            if 'word/document.xml' in docx.namelist():
                tree = ET.fromstring(docx.read('word/document.xml'))
                for paragraph in tree.findall('.//w:p', namespaces):
                    texts = [node.text for node in paragraph.findall('.//w:t', namespaces) if node.text]
                    if texts:
                        text.append(''.join(texts))
    except Exception as e:
        return str(e)
    return '\n'.join(text)

files = [
    "450-500 пътеки.docx",
    "Анализ и корекция на екопътеки ID 201-251.docx",
    "Анализ и обогатяване на данни за екопътеки  302–351.docx",
    "Анализ и обогатяване на данни за екопътеки  463–523.docx",
    "Анализ и подобряване на екопътеки 252–301.docx",
    "Обогатени данни за екопътеки - Пакет 04.docx",
    "Обогатяване и анализ на данни за екопътеки  402–462.docx",
    "Обогатяване на данни за екопътеки ID 151-200.docx",
    "пътеки 1-50.docx",
    "пътеки 101-150.docx",
    "пътеки 51-100.docx"
]

out_dir = r"c:\Users\35987\source\repos\EcoProject\scripts\extracted_texts"
os.makedirs(out_dir, exist_ok=True)

target_dir = r"c:\Users\35987\source\repos\EcoProject\scripts"

for f in files:
    file_path = os.path.join(target_dir, f)
    if not os.path.exists(file_path):
        print(f"File not found: {file_path}")
        continue
    text = extract_text_from_docx(file_path)
    out_path = os.path.join(out_dir, f.replace('.docx', '.txt'))
    with open(out_path, 'w', encoding='utf-8') as out_f:
        out_f.write(text)
    print(f"Extracted {len(text)} chars from {f}")
