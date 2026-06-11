# Dynamic Assistant Prompts - Example Variations

## Weather/Time Prompts Around a Location

**Location:** Киреево

### Possible Variations:
1. **Label:** "Време сега около Киреево"
   **Questions:** 
   - "Кои са текущите условия около Киреево?"
   - "Каква е прогнозата за Киреево в следващите часове?"
   - "Дай ми метеорологични детайли за Киреево?"

2. **Label:** "Текущо време в Киреево"
   **Questions:** (Same variations as above)

3. **Label:** "Проверка на времето около Киреево"
   **Questions:** (Same variations as above)

---

## Personalized Routes in Region

**Region:** Киреево

### Possible Variations:
1. **Label:** "Дай 3 персонализирани маршрута в Киреево"
   **Questions:**
   - "Препоръчай 3 персонализирани маршрута в Киреево с кратко сравнение и ясно предложение кой е най-подходящ за мен."
   - "Кои са топ 3 маршрута в Киреево според моя профил? Защо точно тези?"
   - "Направи ми подреден списък на 3-те най-интересни маршрута в Киреево с причини."

2. **Label:** "Предложи ми 3 маршрута в Киреево"
   **Questions:** (Same variations as above)

3. **Label:** "Кои са 3-те най-добри за мен в Киреево?"
   **Questions:** (Same variations as above)

---

## Trail Comparison

### Possible Variations:
1. **Label:** "Сравни ми топ 2 маршрута около Киреево"
   **Questions:**
   - "Сравни 2-те най-подходящи маршрута около Киреево по трудност, време, денивелация, вода и подходящост за начинаещ."
   - "Кои са найобмислено подобрани 2 маршрута в Киреево? Сравни ги детайлно."
   - "Дай ми дебелинка от топ 2 маршрута в Киреево с ясно съобщение кой е по-добър за мен."

2. **Label:** "Какви са най-добрите 2 маршрута в Киреево?"
   **Questions:** (Same variations as above)

---

## Detailed Advice

### Possible Variations:
1. **Label:** "Искам по-дълъг и детайлен съвет"
   **Questions:**
   - "Дай по-дълъг и подробен отговор с план стъпка по стъпка: маршрут, време, екипировка, рискове и алтернативи."
   - "Разложи ми препоръката с детайли: маршрут → подготовка → опасности → резервни планове."
   - "Обясни всяка препоръка подробно включително техническите характеристики, условията и съветите за безопасност."

2. **Label:** "Дай ми подробна информация"
   **Questions:** (Same variations as above)

---

## How This Works

- **Random Selection:** Each time a new chat session starts, the system randomly selects:
  1. A variation for the label (what user sees)
  2. A variation for the prompt value (what gets sent to AI)

- **No Duplication:** The `Shuffle()` function in BuildQuickActions ensures:
  - Actions are presented in random order
  - Maximum 8 quick actions shown (deduplicated)
  - All required map actions shown first, then optional actions

- **Context-Aware:** Variations are still context-aware:
  - Different locations → different location variations
  - Different regions → different regional variations
  - Same logic, different wording

---

## User Experience Benefit

**Before:** Users saw the same suggestions every session:
- "Време сега около Киреево"
- "Дай 3 персонализирани маршрута"
- "Каква е екипировката за [Trail]?"

**After:** Fresh suggestions each session:
- Session 1: "Текущо време в Киреево", "Предложи ми 3 маршрута", "Какво трябва да нося?"
- Session 2: "Проверка на времето около Киреево", "Кои са 3-те най-добри за мен?", "Екипировка за [Trail]"
- Session 3: Different variations again...

This improves user engagement and reduces monotony.
