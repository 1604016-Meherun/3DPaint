using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ImmersiDraw.Study
{
    // Everything saved for one session. Identified ONLY by study ID - no names, no device serial numbers,
    // no hardware identifiers, no IP addresses, no audio/video/images, no eye tracking.
    [Serializable]
    public class SessionRecord
    {
        public string participantId;          // study ID only, e.g. "P07"
        public int studyDay;                  // 1..5
        public string sessionStartUtc;
        public string sessionEndUtc;
        public string appVersion;

        public string condition;              // "adaptive" or "control"
        public string conditionAssignment;    // e.g. "auto:within_alternating" or "manual_override"
        public string recommenderMechanism;   // "ml_model", "rule_based" or "random_control"
        public string recommenderVersionId;   // content hash of the frozen model / rule set
        public string promptLibraryVersionId; // content hash of the frozen prompt library

        public int preSessionStress = -1;     // 1..5, or -1 if skipped
        public bool preSessionStressSkipped;
        public int postSessionStress = -1;    // only filled when collectPostSessionStressInApp = true
        public bool postSessionStressSkipped;

        public string recommendedPromptId;
        public string recommendedPromptCategory;

        public string completionStatus = "not_started"; // completed / partially_completed / skipped / stopped_early
        public string earlyStopReason = "";
        public double drawingSeconds;          // time in the drawing phase, breaks excluded
        public double breakSeconds;
        public int breakCount;
        public int strokeCount;
        public int helpRequestCount;
        public int tooltipActivationCount;
    }

    // Writes session.json, events.csv and hand_motion.csv to
    // <persistentDataPath>/ImmersiDrawStudy/<participantId>/day<N>_<yyyyMMdd-HHmmss>/
    public class StudyDataLogger : IDisposable
    {
        public readonly string SessionFolder;
        private readonly StreamWriter events;
        private readonly StreamWriter motion;
        private readonly double t0;
        private double lastFlush;

        public static string RootFolder => Path.Combine(Application.persistentDataPath, "ImmersiDrawStudy");

        public StudyDataLogger(string participantId, int day)
        {
            t0 = Time.realtimeSinceStartupAsDouble;
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            SessionFolder = Path.Combine(RootFolder, participantId, "day" + day + "_" + stamp);
            Directory.CreateDirectory(SessionFolder);

            events = new StreamWriter(Path.Combine(SessionFolder, "events.csv"), false, Encoding.UTF8);
            events.WriteLine("t_seconds,event,detail");
            motion = new StreamWriter(Path.Combine(SessionFolder, "hand_motion.csv"), false, Encoding.UTF8);
            motion.WriteLine("t_seconds,hand,pos_x,pos_y,pos_z,rot_x,rot_y,rot_z,rot_w,trigger,is_drawing");
        }

        public double Now => Time.realtimeSinceStartupAsDouble - t0;

        public void Event(string type, string detail = "")
        {
            events.WriteLine(F(Now) + "," + Csv(type) + "," + Csv(detail));
            MaybeFlush();
        }

        public void Motion(string hand, Vector3 p, Quaternion r, float trigger, bool drawing)
        {
            var sb = new StringBuilder(128);
            sb.Append(F(Now)).Append(',').Append(hand).Append(',')
              .Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append(',')
              .Append(F(r.x)).Append(',').Append(F(r.y)).Append(',').Append(F(r.z)).Append(',').Append(F(r.w)).Append(',')
              .Append(F(trigger)).Append(',').Append(drawing ? "1" : "0");
            motion.WriteLine(sb.ToString());
            MaybeFlush();
        }

        public void SaveSession(SessionRecord record)
        {
            File.WriteAllText(Path.Combine(SessionFolder, "session.json"), JsonUtility.ToJson(record, true));
        }

        // Prompt IDs this participant already received in earlier sessions (read from their session.json files).
        public static HashSet<string> PreviouslyUsedPromptIds(string participantId)
        {
            var used = new HashSet<string>();
            string dir = Path.Combine(RootFolder, participantId);
            if (!Directory.Exists(dir)) return used;
            foreach (var file in Directory.GetFiles(dir, "session.json", SearchOption.AllDirectories))
            {
                try
                {
                    var rec = JsonUtility.FromJson<SessionRecord>(File.ReadAllText(file));
                    if (rec != null && !string.IsNullOrEmpty(rec.recommendedPromptId)) used.Add(rec.recommendedPromptId);
                }
                catch { /* ignore unreadable files */ }
            }
            return used;
        }

        private void MaybeFlush()
        {
            if (Now - lastFlush < 1.0) return;   // flush about once per second so little is lost on a crash
            lastFlush = Now;
            events.Flush(); motion.Flush();
        }

        public void Dispose()
        {
            events?.Flush(); events?.Dispose();
            motion?.Flush(); motion?.Dispose();
        }

        private static string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }

    // Static hook other scripts (e.g. LineDrawManager) call. Does nothing when no study session is running.
    public static class StudyEvents
    {
        public static StudySessionManager Session;

        // When a study session is running, drawing is only allowed during the drawing phase
        // (not during setup, the stress prompt, breaks or the end screen).
        public static bool DrawingAllowed => Session == null || Session.IsDrawingPhase;

        public static void Record(string type, string detail = "")
        {
            if (Session != null) Session.RecordEvent(type, detail);
        }
    }
}
