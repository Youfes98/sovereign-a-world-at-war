# AI Roadmap: Unbuilt Systems Reference

> Design reference for future development sessions. Not code — planning only.

---

## Unbuilt Diplomacy Systems

- **Alliance system**: propose/accept/reject alliances, mutual defense obligation, military access grant, AI alliance formation logic based on shared enemies and diplomatic score
- **Escalation ladder (0-6)**:
  - 0 = Peace
  - 1 = Tensions (recall ambassador, denounce)
  - 2 = Proxy conflict (fund rebels, arms deals)
  - 3 = Limited strikes (targeted missile/air strikes)
  - 4 = Full war (conventional ground invasion)
  - 5 = Total war (full mobilization, civilian infrastructure targets)
  - 6 = Nuclear threshold (last resort, massive reputation penalty)
  - Each level gates available actions — cannot jump from 0 to 4
- **Military access**: grant/request via diplomacy panel, AI should grant to allies automatically
- **Loans & debt traps**: offer loans to other countries, call in debts, use debt as leverage for coercion or territory demands
- **Sanctions with economic effect**: reduce target GDP growth by 0.5-1% per sanctioning country, coalition sanctions multiply effect
- **Trade embargo coalitions**: convince allies to jointly embargo a target, UI for proposing coalition embargoes
- **Peace negotiations**:
  - War score based on: territory held + casualties inflicted + war exhaustion
  - AI evaluates and offers/accepts peace when score is unfavorable
  - Peace terms: territory demands, reparations, status quo ante bellum
- **Capitulation**: auto-surrender when >80% core provinces lost, winner gets territory demands fulfilled
- **War goals**: must declare war aims before attacking (conquest, regime change, liberation, etc.), affects peace terms and reputation cost

---

## WorldMemoryDB Integration

- Every war declaration should call `WorldMemoryDB.record()`
- Every treaty break, alliance betrayal, nuclear use should record
- AI should READ reputation before:
  - Declaring war (high aggression score = others form defensive coalition)
  - Offering trade deals (low reliability score = deals rejected)
  - Forming alliances (betrayal history = alliance requests denied)
- Diplomatic score calculation should factor in all reputation axes
- Historical memory decay: recent events weighted more than old ones

---

## AI Improvements (Future)

- **Force concentration**: identify weakest enemy border segment, send 2-3 armies to overwhelm
- **Defensive posture**: keep 1 army as reserve at capital, garrison key border provinces
- **Strategic terrain**: prefer defending on mountains/rivers, avoid attacking into mountains
- **Army regrouping**: retreat when below 40% strength, recover in friendly territory before re-engaging
- **Air deployment**: deploy fighters/bombers to active fronts within range, prioritize air superiority
- **Naval strategy**: blockade enemy ports, plan amphibious invasions, protect sea lanes
- **Threat assessment**: detect powerful hostile neighbor, build up defensively before conflict
- **Coalition warfare**: coordinate attacks with allies against shared enemies, avoid friendly fire zones

---

## Economy Improvements (Future)

- **AI tax adjustment**: raise taxes when running deficit, lower when surplus is stable
- **AI budget reallocation**: shift spending to military in wartime, infrastructure in peacetime
- **Debt restructuring**: at >150% debt-to-GDP, trigger IMF-style event — slash debt by 50% but crash stability and credit rating
- **Trade deal AI**: propose trade deals to high-GDP neighbors with positive relations
- **Building upgrade system**: buildings should have levels (1-3), AI should upgrade existing buildings before building new ones

---

## UI/UX For New Systems

- Alliance proposal dialog (propose terms, review obligations, accept/reject)
- Peace negotiation screen with territory demands on map
- Escalation level indicator on country relations panel
- Loan management panel (outstanding debts, interest rates, leverage options)
- War goals declaration screen (select aims before declaring war)
