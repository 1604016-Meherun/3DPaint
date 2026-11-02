# ImmersiDraw IRB study mode

These scripts add the session flow and data logging described in the adult IRB protocol.
They are created automatically at runtime in any scene that has a `LineDrawManager`
(the Drawing Scene), so **no scene or prefab changes are required**.

## Conditions (adaptive vs. control)
Every session runs in one of two conditions. Both look identical to the participant (same stress question,
same prompt screen, same prompt library of matched difficulty); only the way the prompt is chosen differs.

| Condition | How the prompt is chosen | `recommenderMechanism` in the log |
|---|---|---|
| **adaptive** | from the pre-session stress rating (rule-based mapping, or a frozen model if one is added) | `rule_based` / `ml_model` |
| **control** | a random prompt category (equal chance), ignoring the stress rating; seeded by study ID + day so it is reproducible | `random_control` |

Assignment is set by `conditionDesign` in `study_settings.json`:
- `within_alternating` (default) – everyone gets both, alternating by day. Odd study IDs: A-C-A-C-A; even IDs: C-A-C-A-C.
  Recruit an even number of participants so the two orders are balanced.
- `between` – odd study IDs always adaptive, even IDs always control.
- `adaptive_only` – no control condition.

The setup screen shows the condition for the researcher, with a **Switch condition** button for a manual
override (logged as `manual_override`). Do not tell the participant which condition they are in.

## Session flow (in the headset)
1. **Researcher setup** – choose study ID (P01–P99) and day (1–5) and check the condition. No names are ever entered.
2. **Pre-session stress prompt** – "How stressed do you feel right now?" 1 = Not at all stressed … 5 = Extremely stressed, with **Continue** and **Skip**.
3. **Recommended prompt** – one prompt from the frozen prompt library, chosen from the stress rating
   (skipping gives the library's default category). Each participant gets a prompt they have not had before.
4. **Drawing** (drawing is disabled at all other times). Side panel (left controller): **Help**, **I'm finished**, **Stop session**.
   - Mandatory **break** every `breakIntervalMinutes` of drawing ("Please remove the headset and rest" → researcher presses **Resume**).
   - Session ends as *partially_completed* when `drawingMinutes` (wall time, including breaks) runs out.
   - **Stop session** records the reason: discomfort / participant chose to stop / technical / skip this prompt.
5. Optional in-app **post-session stress rating** (`collectPostSessionStressInApp`; default off because it is on the paper survey).

Buttons work with the XR ray, or with a thumbstick to move + **A/X** to press
(during drawing only the left controller navigates, so the right hand can draw).

## Data saved (on the headset)
`<Application.persistentDataPath>/ImmersiDrawStudy/<StudyID>/day<N>_<UTC timestamp>/`
(on Quest: `/sdcard/Android/data/<package name>/files/ImmersiDrawStudy/`)

| File | Contents |
|---|---|
| `session.json` | study ID, day, **condition** + how it was assigned, start/end time, recommender mechanism + version ID, prompt-library version ID, pre/post stress (or skipped), recommended prompt ID + category, completion status, early-stop reason, drawing time, break count/time, stroke count, help requests, tooltip activations |
| `events.csv` | time-stamped events: session/task start & end, stress ratings, prompt recommended, stroke start/end (points, duration, color), color selections, undo, erase, controller button actions, help requests, tooltip activations, breaks, time limit, early stop |
| `hand_motion.csv` | left/right **controller** position + orientation and trigger value, sampled at `motionSampleHz` during drawing |

**Not collected:** audio, video, images, eye tracking, hand-tracking/biometric data, IP addresses,
device serial numbers or other hardware identifiers.

## Frozen recommender and prompt library
- `Resources/ImmersiDrawStudy/prompt_library.json` – 15 prompts of similar (simple) complexity in 3 categories.
- Version IDs are SHA-256 hashes of the exact file contents and are written to every session, so any edit
  to the library or model automatically shows up as a different version in the logs.
- Nothing is ever trained or updated from participant data; all processing is on the headset, with no network calls.
- **Recommender mechanism:** if `Resources/ImmersiDrawStudy/recommender_model.json` exists, a frozen
  multinomial logistic-regression model is used (`recommenderMechanism = "ml_model"`); format is documented in
  `PromptRecommender.cs`. **No trained model is included yet**, so the app currently uses the fixed rule-based mapping
  (1–2 → creative exploration, 3 → calm nature, 4–5 → slow grounding; skipped → calm nature) and logs
  `recommenderMechanism = "rule_based"`. Either add a model trained offline (not on participant data) before the
  study, or describe the mechanism as rule-based in the IRB documents.

## Settings
`Resources/ImmersiDrawStudy/study_settings.json` – set `"studyModeEnabled": false` to use the app without the study flow.
