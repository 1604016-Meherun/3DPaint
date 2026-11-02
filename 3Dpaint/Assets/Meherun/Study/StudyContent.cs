using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ImmersiDraw.Study
{
    // ------------------------------------------------------------------
    // Study settings (Resources/ImmersiDrawStudy/study_settings.json)
    // ------------------------------------------------------------------
    [Serializable]
    public class StudySettings
    {
        public bool studyModeEnabled = true;
        public float drawingMinutes = 20f;          // VR drawing time per session, including breaks
        public float breakIntervalMinutes = 10f;    // a mandatory break is shown after this much drawing time
        public float motionSampleHz = 30f;          // hand (controller) pose sampling rate
        public bool collectPostSessionStressInApp = false; // false = post-session rating is on the paper survey
        public float panelDistanceMeters = 1.2f;
    }

    // ------------------------------------------------------------------
    // Prompt library (Resources/ImmersiDrawStudy/prompt_library.json)
    // ------------------------------------------------------------------
    [Serializable]
    public class DrawingPrompt
    {
        public string id;
        public string category;
        public string complexity;
        public string text;
    }

    [Serializable]
    public class PromptLibraryData
    {
        public string[] categories;
        public string defaultCategory;   // used when the participant skips the stress question
        public DrawingPrompt[] prompts;
    }

    public static class StudyContent
    {
        public const string ResourceFolder = "ImmersiDrawStudy/";

        public static StudySettings LoadSettings()
        {
            var asset = Resources.Load<TextAsset>(ResourceFolder + "study_settings");
            if (asset == null) return new StudySettings();
            try { return JsonUtility.FromJson<StudySettings>(asset.text) ?? new StudySettings(); }
            catch (Exception e)
            {
                Debug.LogError("ImmersiDraw study: could not parse study_settings.json: " + e.Message);
                return new StudySettings();
            }
        }

        // Loads the frozen prompt library. The version identifier is a hash of the file's exact
        // contents, so any edit to the library automatically produces a different identifier in the logs.
        public static PromptLibraryData LoadPromptLibrary(out string versionId)
        {
            versionId = "missing";
            var asset = Resources.Load<TextAsset>(ResourceFolder + "prompt_library");
            if (asset == null)
            {
                Debug.LogError("ImmersiDraw study: prompt_library.json not found in Resources/" + ResourceFolder);
                return null;
            }
            versionId = "lib-" + ShortHash(asset.text);
            return JsonUtility.FromJson<PromptLibraryData>(asset.text);
        }

        public static string ShortHash(string text)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
                var sb = new StringBuilder();
                for (int i = 0; i < 6; i++) sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
