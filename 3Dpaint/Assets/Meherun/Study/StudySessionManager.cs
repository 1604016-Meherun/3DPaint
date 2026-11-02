using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using static ImmersiDraw.Study.StudyPanelUI;

namespace ImmersiDraw.Study
{
    // Runs one IRB study session in the headset:
    //   researcher setup (study ID + day) -> pre-session stress rating -> recommended prompt ->
    //   drawing (with mandatory breaks, help, finish/stop) -> optional post-session rating -> end.
    // Created automatically in any scene that has a LineDrawManager when study mode is enabled
    // in Resources/ImmersiDrawStudy/study_settings.json - no scene changes are needed.
    public class StudySessionManager : MonoBehaviour
    {
        enum Phase { Setup, PreStress, PromptReveal, Drawing, Break, ConfirmFinish, StopReason, PostStress, Ended }

        StudySettings settings;
        PromptLibraryData library;
        string libraryVersionId;
        IPromptRecommender adaptiveRecommender;
        RandomControlRecommender controlRecommender;
        IPromptRecommender recommender;     // the one used in the current session
        string conditionOverride;           // null = automatic assignment

        StudyPanelUI ui;
        StudyDataLogger log;
        SessionRecord record;
        Phase phase = Phase.Setup;

        // setup
        int participantNumber = 1;
        int day = 1;
        int? pendingRating;           // rating currently selected on the stress screen
        DrawingPrompt prompt;

        // timing (seconds)
        double drawingStartWall;      // wall time the drawing phase first started (for the total time limit)
        double drawingSecondsSinceBreak;
        double breakStart;
        double nextMotionSample;

        // controller action edge detection: [hand][button]
        static readonly string[] ButtonNames = { "trigger", "grip", "primary", "secondary", "menu", "thumbstick_click" };
        readonly bool[,] prevButtons = new bool[2, 6];

        public bool IsDrawingPhase => phase == Phase.Drawing;
        string ParticipantId => "P" + participantNumber.ToString("00", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ bootstrap
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            TryCreateForScene();
            SceneManager.sceneLoaded += (s, m) => TryCreateForScene();
        }

        static void TryCreateForScene()
        {
            if (StudyEvents.Session != null) return;
            if (!StudyContent.LoadSettings().studyModeEnabled) return;
            if (FindFirstObjectByType<LineDrawManager>() == null) return;   // only in the drawing scene
            new GameObject("ImmersiDraw Study Session").AddComponent<StudySessionManager>();
        }

        // ------------------------------------------------------------------ lifecycle
        void Awake()
        {
            StudyEvents.Session = this;
            settings = StudyContent.LoadSettings();
            library = StudyContent.LoadPromptLibrary(out libraryVersionId);
            if (library == null || library.prompts == null || library.prompts.Length == 0)
            {
                Debug.LogError("ImmersiDraw study: no prompt library - study mode disabled.");
                StudyEvents.Session = null;
                Destroy(gameObject);
                return;
            }
            adaptiveRecommender = RecommenderFactory.Create(library);
            controlRecommender = new RandomControlRecommender(library);
            Debug.Log($"ImmersiDraw study: adaptive={adaptiveRecommender.MechanismType} {adaptiveRecommender.VersionId}, " +
                      $"control={controlRecommender.VersionId}, library={libraryVersionId}, design={settings.conditionDesign}");
        }

        void Start()
        {
            if (StudyEvents.Session != this) return;   // disabled in Awake (no prompt library)
            ui = StudyPanelUI.Create();
            ShowSetup();
        }

        void OnDestroy()
        {
            if (StudyEvents.Session == this) StudyEvents.Session = null;
            if (log != null && phase != Phase.Ended) EndSession("stopped_early", "app_closed");
        }

        void OnApplicationQuit()
        {
            if (log != null && phase != Phase.Ended) EndSession("stopped_early", "app_closed");
        }

        // ------------------------------------------------------------------ screens
        string AutoCondition => ConditionAssignment.Assign(settings.conditionDesign, participantNumber, day);
        string CurrentCondition => conditionOverride ?? AutoCondition;

        void ShowSetup()
        {
            phase = Phase.Setup;
            ui.LeftHandOnly = false;
            string warning = SessionAlreadyExists(ParticipantId, day)
                ? "\n<color=#FFC857>A session for this study ID and day already exists.</color>" : "";
            string condLabel = CurrentCondition == ConditionAssignment.Adaptive ? "ADAPTIVE" : "CONTROL (random prompt)";
            string condSource = conditionOverride == null ? $"automatic: {settings.conditionDesign}" : "<color=#FFC857>manual override</color>";
            ui.Show("Researcher setup",
                $"Study ID: <b>{ParticipantId}</b>     Day: <b>{day}</b> of 5{warning}\n" +
                $"<size=26>Condition: <b>{condLabel}</b> ({condSource})</size>\n" +
                "<size=24>Do not tell the participant the condition. Use study ID numbers only - no names.</size>",
                new[] { new ButtonSpec("ID -", () => { participantNumber = Mathf.Max(1, participantNumber - 1); conditionOverride = null; ShowSetup(); }),
                        new ButtonSpec("ID +", () => { participantNumber = Mathf.Min(99, participantNumber + 1); conditionOverride = null; ShowSetup(); }),
                        new ButtonSpec("Day -", () => { day = Mathf.Max(1, day - 1); conditionOverride = null; ShowSetup(); }),
                        new ButtonSpec("Day +", () => { day = Mathf.Min(5, day + 1); conditionOverride = null; ShowSetup(); }) },
                new[] { new ButtonSpec("Switch condition", () =>
                        {
                            conditionOverride = CurrentCondition == ConditionAssignment.Adaptive
                                ? ConditionAssignment.Control : ConditionAssignment.Adaptive;
                            if (conditionOverride == AutoCondition) conditionOverride = null;
                            ShowSetup();
                        }),
                        new ButtonSpec("Start session", StartSession) });
            ui.PlaceInFrontOfUser(settings.panelDistanceMeters);
        }

        void StartSession()
        {
            string condition = CurrentCondition;
            if (condition == ConditionAssignment.Control)
            {
                controlRecommender.ParticipantId = ParticipantId;
                controlRecommender.Day = day;
                recommender = controlRecommender;
            }
            else recommender = adaptiveRecommender;

            log = new StudyDataLogger(ParticipantId, day);
            record = new SessionRecord
            {
                participantId = ParticipantId,
                studyDay = day,
                sessionStartUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                appVersion = Application.version,
                condition = condition,
                conditionAssignment = conditionOverride == null ? "auto:" + settings.conditionDesign : "manual_override",
                recommenderMechanism = recommender.MechanismType,
                recommenderVersionId = recommender.VersionId,
                promptLibraryVersionId = libraryVersionId,
            };
            log.Event("session_start", $"day={day};condition={condition};recommender={recommender.MechanismType}:{recommender.VersionId};library={libraryVersionId}");
            log.SaveSession(record);
            pendingRating = null;
            ShowStressScreen(pre: true);
        }

        // The exact in-app stress prompt described in the IRB protocol.
        void ShowStressScreen(bool pre)
        {
            phase = pre ? Phase.PreStress : Phase.PostStress;
            ui.LeftHandOnly = false;
            string[] labels = { "1\nNot at all", "2\nSlightly", "3\nModerately", "4\nVery", "5\nExtremely" };
            var ratingRow = new ButtonSpec[5];
            for (int i = 0; i < 5; i++)
            {
                int value = i + 1;
                ratingRow[i] = new ButtonSpec(labels[i], () => { pendingRating = value; ShowStressScreen(pre); },
                                              highlighted: pendingRating == value);
            }
            ui.Show(pre ? "Before You Begin" : "Before You Finish",
                "How stressed do you feel right now?\n<size=24>1 = Not at all stressed      5 = Extremely stressed</size>",
                ratingRow,
                new[] { new ButtonSpec("Continue", () => SubmitStress(pre, pendingRating), interactable: pendingRating.HasValue),
                        new ButtonSpec("Skip", () => SubmitStress(pre, null)) });
            ui.PlaceInFrontOfUser(settings.panelDistanceMeters);
        }

        void SubmitStress(bool pre, int? rating)
        {
            string tag = pre ? "pre_session_stress" : "post_session_stress";
            log.Event(tag, rating.HasValue ? rating.Value.ToString(CultureInfo.InvariantCulture) : "skipped");
            if (pre)
            {
                record.preSessionStress = rating ?? -1;
                record.preSessionStressSkipped = !rating.HasValue;
                var used = StudyDataLogger.PreviouslyUsedPromptIds(ParticipantId);
                prompt = recommender.Recommend(rating, used);
                record.recommendedPromptId = prompt.id;
                record.recommendedPromptCategory = prompt.category;
                log.Event("prompt_recommended", $"{prompt.id};{prompt.category};{recommender.MechanismType}:{recommender.VersionId}");
                log.SaveSession(record);
                pendingRating = null;
                ShowPromptReveal();
            }
            else
            {
                record.postSessionStress = rating ?? -1;
                record.postSessionStressSkipped = !rating.HasValue;
                FinishAndShowEnd();
            }
        }

        void ShowPromptReveal()
        {
            phase = Phase.PromptReveal;
            ui.Show("Today's drawing prompt", prompt.text,
                new[] { new ButtonSpec("Start drawing", StartDrawing) });
            ui.PlaceInFrontOfUser(settings.panelDistanceMeters);
        }

        void StartDrawing()
        {
            if (record.completionStatus == "not_started")
            {
                drawingStartWall = Time.realtimeSinceStartupAsDouble;
                record.completionStatus = "in_progress";
                log.Event("task_start", prompt.id);
            }
            else log.Event("drawing_resumed");
            phase = Phase.Drawing;
            ShowDrawingToolbar();
        }

        // Small side panel while drawing. Navigated with the LEFT controller only, so the right hand can draw.
        void ShowDrawingToolbar()
        {
            ui.LeftHandOnly = true;
            ui.Show("Prompt", prompt.text,
                new[] { new ButtonSpec("Help", ShowHelp),
                        new ButtonSpec("I'm finished", AskFinish) },
                new[] { new ButtonSpec("Stop session", ShowStopReasons) });
            ui.PlaceInFrontOfUser(settings.panelDistanceMeters, yawOffsetDegrees: -45f, heightOffset: -0.25f, scaleFactor: 0.6f);
        }

        void ShowHelp()
        {
            record.helpRequestCount++;
            record.tooltipActivationCount++;
            log.Event("help_request", "help_panel");
            log.Event("tooltip_activation", "drawing_help");
            ui.Show("How to draw",
                "Hold the <b>right trigger</b> to draw.\nUse the color buttons to change color.\n<b>Undo</b> removes your last stroke.\nYou can take a break or stop at any time.",
                new[] { new ButtonSpec("Back to drawing", ShowDrawingToolbar) });
            ui.PlaceInFrontOfUser(settings.panelDistanceMeters, yawOffsetDegrees: -45f, heightOffset: -0.25f, scaleFactor: 0.6f);
        }

        void AskFinish()
        {
            phase = Phase.ConfirmFinish;
            ui.Show("Are you finished with today's drawing?", "",
                new[] { new ButtonSpec("Yes, I'm finished", () => CompleteTask("completed")),
                        new ButtonSpec("No, keep drawing", () => { phase = Phase.Drawing; ShowDrawingToolbar(); }) });
            ui.PlaceInFrontOfUser(settings.panelDistanceMeters, yawOffsetDegrees: -45f, heightOffset: -0.25f, scaleFactor: 0.6f);
        }

        void ShowStopReasons()
        {
            phase = Phase.StopReason;
            ui.LeftHandOnly = false;
            ui.Show("Stop the session?", "Choose the reason (for the research record).",
                new[] { new ButtonSpec("Discomfort", () => StopEarly("discomfort")),
                        new ButtonSpec("Participant chose to stop", () => StopEarly("participant_request")) },
                new[] { new ButtonSpec("Technical problem", () => StopEarly("technical")),
                        new ButtonSpec("Skip this prompt", () => CompleteTask("skipped")) },
                new[] { new ButtonSpec("Cancel - keep drawing", () => { phase = Phase.Drawing; ShowDrawingToolbar(); }) });
            ui.PlaceInFrontOfUser(settings.panelDistanceMeters);
        }

        void ShowBreak()
        {
            phase = Phase.Break;
            ui.LeftHandOnly = false;
            breakStart = Time.realtimeSinceStartupAsDouble;
            record.breakCount++;
            log.Event("break_start", "mandatory");
            ui.Show("Break time",
                "Please remove the headset and rest.\nThe researcher will help you continue when you are ready.",
                new[] { new ButtonSpec("Resume drawing", EndBreak),
                        new ButtonSpec("Stop session", ShowStopReasons) });
            ui.PlaceInFrontOfUser(settings.panelDistanceMeters);
        }

        void EndBreak()
        {
            double secs = Time.realtimeSinceStartupAsDouble - breakStart;
            record.breakSeconds += secs;
            drawingSecondsSinceBreak = 0;
            log.Event("break_end", secs.ToString("0.0", CultureInfo.InvariantCulture));
            phase = Phase.Drawing;
            ShowDrawingToolbar();
        }

        void StopEarly(string reason)
        {
            if (phase == Phase.Break) EndBreakSilently();
            record.earlyStopReason = reason;
            log.Event("session_stopped_early", reason);
            EndSession("stopped_early", reason);
            ShowEnd();
        }

        void CompleteTask(string status)
        {
            log.Event("task_end", status);
            record.completionStatus = status;
            if (settings.collectPostSessionStressInApp) { pendingRating = null; ShowStressScreen(pre: false); }
            else FinishAndShowEnd();
        }

        void FinishAndShowEnd()
        {
            EndSession(record.completionStatus, "");
            ShowEnd();
        }

        void ShowEnd()
        {
            phase = Phase.Ended;
            ui.LeftHandOnly = false;
            ui.Show("Thank you!",
                "Today's session is complete.\nPlease remove the headset and complete the short survey.",
                new[] { new ButtonSpec("Researcher: new session", () => { day = Mathf.Min(5, day); ShowSetup(); }) });
            ui.PlaceInFrontOfUser(settings.panelDistanceMeters);
        }

        // ------------------------------------------------------------------ per-frame
        void Update()
        {
            if (log == null) return;
            if (phase != Phase.Drawing) return;

            drawingSecondsSinceBreak += Time.unscaledDeltaTime;
            record.drawingSeconds += Time.unscaledDeltaTime;

            SampleControllers();

            double wall = Time.realtimeSinceStartupAsDouble - drawingStartWall;
            if (wall >= settings.drawingMinutes * 60.0)
            {
                log.Event("time_limit_reached", settings.drawingMinutes.ToString(CultureInfo.InvariantCulture));
                CompleteTask("partially_completed");
                return;
            }
            if (settings.breakIntervalMinutes > 0 && drawingSecondsSinceBreak >= settings.breakIntervalMinutes * 60.0)
                ShowBreak();
        }

        void SampleControllers()
        {
            bool sample = Time.realtimeSinceStartupAsDouble >= nextMotionSample;
            if (sample) nextMotionSample = Time.realtimeSinceStartupAsDouble + 1.0 / Mathf.Max(1f, settings.motionSampleHz);

            for (int h = 0; h < 2; h++)
            {
                var node = h == 0 ? XRNode.LeftHand : XRNode.RightHand;
                string hand = h == 0 ? "left" : "right";
                var dev = InputDevices.GetDeviceAtXRNode(node);
                if (!dev.isValid) continue;

                // controller actions (button down / up edges)
                bool[] now = new bool[6];
                dev.TryGetFeatureValue(CommonUsages.triggerButton, out now[0]);
                dev.TryGetFeatureValue(CommonUsages.gripButton, out now[1]);
                dev.TryGetFeatureValue(CommonUsages.primaryButton, out now[2]);
                dev.TryGetFeatureValue(CommonUsages.secondaryButton, out now[3]);
                dev.TryGetFeatureValue(CommonUsages.menuButton, out now[4]);
                dev.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out now[5]);
                for (int b = 0; b < 6; b++)
                {
                    if (now[b] != prevButtons[h, b])
                        log.Event("controller_action", $"{hand}:{ButtonNames[b]}_{(now[b] ? "down" : "up")}");
                    prevButtons[h, b] = now[b];
                }

                // hand movement: controller position + orientation only (no hand tracking, no biometrics)
                if (sample &&
                    dev.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos) &&
                    dev.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
                {
                    dev.TryGetFeatureValue(CommonUsages.trigger, out float trig);
                    log.Motion(hand, pos, rot, trig, now[0] && h == 1);
                }
            }
        }

        // ------------------------------------------------------------------ events from other scripts
        public void RecordEvent(string type, string detail)
        {
            if (log == null) return;
            if (type == "stroke_end") record.strokeCount++;
            log.Event(type, detail);
        }

        // ------------------------------------------------------------------ helpers
        void EndBreakSilently()
        {
            record.breakSeconds += Time.realtimeSinceStartupAsDouble - breakStart;
            log.Event("break_end", "stopped_during_break");
        }

        void EndSession(string status, string reason)
        {
            if (log == null) return;
            record.completionStatus = status;
            if (!string.IsNullOrEmpty(reason) && status == "stopped_early") record.earlyStopReason = reason;
            record.sessionEndUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            log.Event("session_end", status);
            log.SaveSession(record);
            Debug.Log("ImmersiDraw study: session saved to " + log.SessionFolder);
            log.Dispose();
            log = null;
            record = null;
            prompt = null;
            conditionOverride = null;
            drawingSecondsSinceBreak = 0;
        }

        static bool SessionAlreadyExists(string participantId, int day)
        {
            string dir = Path.Combine(StudyDataLogger.RootFolder, participantId);
            if (!Directory.Exists(dir)) return false;
            return Directory.GetDirectories(dir, "day" + day + "_*").Length > 0;
        }
    }
}
