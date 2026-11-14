import requests
import json

def extract_text_from_image(image_path, api_key, model, task_type, max_tokens, temperature, top_p, repetition_penalty, pages=None):
    url = "https://api.opentyphoon.ai/v1/ocr"

    with open(image_path, 'rb') as file:
        files = {'file': file}
        data = {
            'model': model,
            'task_type': task_type,
            'max_tokens': str(max_tokens),
            'temperature': str(temperature),
            'top_p': str(top_p),
            'repetition_penalty': str(repetition_penalty)
        }

        if pages:
            data['pages'] = json.dumps(pages)

        headers = {
            'Authorization': f'Bearer {api_key}'
        }

        response = requests.post(url, files=files, data=data, headers=headers)

        if response.status_code == 200:
            result = response.json()

            # Extract text from successful results
            extracted_texts = []
            for page_result in result.get('results', []):
                if page_result.get('success') and page_result.get('message'):
                    content = page_result['message']['choices'][0]['message']['content']
                    try:
                        # Try to parse as JSON if it's structured output
                        parsed_content = json.loads(content)
                        text = parsed_content.get('natural_text', content)
                    except json.JSONDecodeError:
                        text = content
                    extracted_texts.append(text)
                elif not page_result.get('success'):
                    print(f"Error processing {page_result.get('filename', 'unknown')}: {page_result.get('error', 'Unknown error')}")

            return '\n'.join(extracted_texts)
        else:
            print(f"Error: {response.status_code}")
            print(response.text)
            return None

# Usage
api_key = "sk-eOYhgdCMhrr6dK0tHqbTIIUJoOdkZlBvbZjGlByoO82LImTP"  #api typhoonOCR ที่ดึงมาใช้
image_path = "./วุฒิบัตร.png"  # or path/to/your/document.pdf
model = "typhoon-ocr"
task_type = "default"
max_tokens = 16384
temperature = 0.1
top_p = 0.6
repetition_penalty = 1.2
pages = None
extracted_text = extract_text_from_image(image_path, api_key, model, task_type, max_tokens, temperature, top_p, repetition_penalty, pages)
#print(extracted_text)


import re
from datetime import datetime

#แปลงเลขไทยเป็นอารบิก
def thai_digit_to_arabic(text: str) -> str:
    thai_digits = "๐๑๒๓๔๕๖๗๘๙"
    arabic_digits = "0123456789"
    return text.translate(str.maketrans(thai_digits, arabic_digits))

def verify_certificate_text(raw_text: str, max_age_years: int = 1) -> dict:
    """ฟังก์ชันตรวจข้อความที่ได้จาก Typhoon OCR ตามเงื่อนไข"""
    if not raw_text or not raw_text.strip():
        return {
            "is_valid": False,
            "reason": "ไม่สามารถอ่านข้อความจากรูปภาพได้ หรือรูปภาพไม่มีข้อความ"
        }

    #แปลงเลขไทยทั้งหมดในข้อความเป็นเลขอารบิก
    normalized_text = thai_digit_to_arabic(raw_text)

    #ตรวจสอบว่าเป็นเกียรติบัตรจริงไหม
    cert_keywords = ["เกียรติบัตร", "วุฒิบัตร" , "รวมใจไทย ปลูกต้นไม้ เพื่อแผ่นดิน"]
    has_cert_keyword = any(kw.lower() in normalized_text.lower() for kw in cert_keywords)

    if not has_cert_keyword:
        return {
            "is_valid": False,
            "reason": "ไม่พบลักษณะข้อความที่เป็นเกียรติบัตร",
            "extracted_text": normalized_text
        }

    #สกัดปี พ.ศ. หรือ ค.ศ.
    buddhist_years = [int(y) for y in re.findall(r"\b(25\d{2})\b", normalized_text)]
    gregorian_years = [int(y) + 543 for y in re.findall(r"\b(20\d{2})\b", normalized_text)]
    all_years = buddhist_years + gregorian_years

    if not all_years:
        return {
            "is_valid": False,
            "reason": "ไม่พบการระบุปี พ.ศ. หรือ ค.ศ. บนเกียรติบัตร",
            "extracted_text": normalized_text
        }

    cert_year = max(all_years)

    #ตรวจสอบอายุเกียรติบัตรเทียบกับปีปัจจุบัน
    current_year_be = datetime.now().year + 543
    min_allowed_year = current_year_be - max_age_years

    if cert_year < min_allowed_year:
        return {
            "is_valid": False,
            "reason": f"เกียรติบัตรหมดอายุ (ปีที่ออก: พ.ศ. {cert_year} เก่าเกินกว่าเกณฑ์ย้อนหลัง {max_age_years} ปี)",
            "year": cert_year
        }

    return {
        "is_valid": True,
        "reason": "ผ่านการตรวจสอบ เงื่อนไขถูกต้อง",
        "year": cert_year,
        "current_year": current_year_be
    }

# ตรวจสอบตัวแปร extracted_text จากเซลล์ก่อนหน้า
if 'extracted_text' in globals() and extracted_text:
    verification_result = verify_certificate_text(extracted_text, max_age_years=1)
    print("\n" + " สรุปผลการตรวจ " )
    print("สถานะผ่านเกณฑ์:", "ผ่าน" if verification_result["is_valid"] else "ไม่ผ่าน")
    print(verification_result["reason"])
    if "year" in verification_result:
        print("ปี พ.ศ. ที่ตรวจพบ:", verification_result["year"])
