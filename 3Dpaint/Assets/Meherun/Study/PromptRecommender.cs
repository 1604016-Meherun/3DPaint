using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiDraw.Study
{
    // Chooses one drawing prompt from the frozen prompt library based on the pre-session stress rating.
    // Recommenders never learn from participant data: they are loaded once and never modified at runtime.
    public interface IPromptRecommender
    {
        string MechanismType { get; }  // "ml_model" or "rule_based" - written to every session log
        string VersionId { get; }      // content hash of the model / rule set - written to every session log
        DrawingPrompt Recommend(int? stressRating, ICollection<string> previouslyUsedPromptIds);
    }

    public static class RecommenderFactory
    {
        // Uses a frozen ML model if Resources/ImmersiDrawStudy/recommender_model.json exists;
        // otherwise falls back to the fixed rule-based mapping. The mechanism actually used is logged.
        public static IPromptRecommender Create(PromptLibraryData library)
        {
            var modelAsset = Resources.Load<TextAsset>(StudyContent.ResourceFolder + "recommender_model");
            if (modelAsset != null)
            {
                var model = FrozenLinearModelRecommender.TryLoad(modelAsset.text, library);
                if (model != null) return model;
                Debug.LogWarning("ImmersiDraw study: recommender_model.json is invalid - using rule-based recommender.");
            }
            return new RuleBasedRecommender(library);
        }

        // Within a category, pick the first prompt (in library order) this participant has not had yet,
        // so each participant gets a different prompt each day. Deterministic and reproducible.
        internal static DrawingPrompt PickFromCategory(PromptLibraryData library, string category, ICollection<string> used)
        {
            DrawingPrompt firstInCategory = null;
            foreach (var p in library.prompts)
            {
                if (p.category != category) continue;
                if (firstInCategory == null) firstInCategory = p;
                if (used == null || !used.Contains(p.id)) return p;
            }
            return firstInCategory ?? library.prompts[0];
        }
    }

    // Fixed mapping: low stress -> creative exploration, moderate -> calm nature, high -> slow grounding.
    public class RuleBasedRecommender : IPromptRecommender
    {
        private readonly PromptLibraryData library;
        public string MechanismType => "rule_based";
        public string VersionId { get; }

        public RuleBasedRecommender(PromptLibraryData library)
        {
            this.library = library;
            VersionId = "rules-" + StudyContent.ShortHash("1-2:creative_exploration;3:nature_calm;4-5:grounding_rhythm;skip:default");
        }

        public DrawingPrompt Recommend(int? stress, ICollection<string> used)
        {
            string category;
            if (!stress.HasValue) category = library.defaultCategory;
            else if (stress.Value <= 2) category = "creative_exploration";
            else if (stress.Value == 3) category = "nature_calm";
            else category = "grounding_rhythm";
            return RecommenderFactory.PickFromCategory(library, category, used);
        }
    }

    // A frozen multinomial logistic-regression model trained offline (never on study participants' data).
    // recommender_model.json format:
    // { "features": ["bias","s1","s2","s3","s4","s5","skipped"],
    //   "categories": ["creative_exploration","nature_calm","grounding_rhythm"],
    //   "weights": [ {"values":[...7 numbers...]}, {"values":[...]}, {"values":[...]} ] }
    public class FrozenLinearModelRecommender : IPromptRecommender
    {
#pragma warning disable 0649 // fields are filled by JsonUtility
        [Serializable] private class Row { public float[] values; }
        [Serializable] private class ModelFile { public string[] features; public string[] categories; public Row[] weights; }
#pragma warning restore 0649

        private readonly PromptLibraryData library;
        private readonly ModelFile model;
        public string MechanismType => "ml_model";
        public string VersionId { get; }

        private FrozenLinearModelRecommender(PromptLibraryData library, ModelFile model, string versionId)
        {
            this.library = library; this.model = model; VersionId = versionId;
        }

        public static FrozenLinearModelRecommender TryLoad(string json, PromptLibraryData library)
        {
            try
            {
                var m = JsonUtility.FromJson<ModelFile>(json);
                if (m == null || m.categories == null || m.weights == null || m.weights.Length != m.categories.Length) return null;
                foreach (var r in m.weights) if (r.values == null || r.values.Length != 7) return null;
                return new FrozenLinearModelRecommender(library, m, "model-" + StudyContent.ShortHash(json));
            }
            catch { return null; }
        }

        public DrawingPrompt Recommend(int? stress, ICollection<string> used)
        {
            // features: bias, one-hot stress 1..5, skipped
            var x = new float[7];
            x[0] = 1f;
            if (stress.HasValue && stress.Value >= 1 && stress.Value <= 5) x[stress.Value] = 1f; else x[6] = 1f;

            int best = 0; float bestScore = float.NegativeInfinity;
            for (int c = 0; c < model.categories.Length; c++)
            {
                float s = 0f;
                for (int i = 0; i < 7; i++) s += model.weights[c].values[i] * x[i];
                if (s > bestScore) { bestScore = s; best = c; }   // argmax of softmax = argmax of logits
            }
            return RecommenderFactory.PickFromCategory(library, model.categories[best], used);
        }
    }

    // CONTROL condition: ignores the stress rating and picks a prompt category at random (equal chance),
    // then a prompt this participant has not had yet. Prompts are drawn from the SAME frozen library, so
    // prompt difficulty is matched between conditions. The random draw is seeded by study ID + day, so the
    // choice is reproducible and can be audited later.
    public class RandomControlRecommender : IPromptRecommender
    {
        private readonly PromptLibraryData library;
        public string MechanismType => "random_control";
        public string VersionId { get; }
        public string ParticipantId = "";
        public int Day;

        public RandomControlRecommender(PromptLibraryData library)
        {
            this.library = library;
            VersionId = "random-" + StudyContent.ShortHash("uniform-category;seed=participantId+day;v1");
        }

        public DrawingPrompt Recommend(int? stress, ICollection<string> used)
        {
            var rng = new System.Random(StableSeed(ParticipantId + "|" + Day));
            string category = library.categories[rng.Next(library.categories.Length)];
            return RecommenderFactory.PickFromCategory(library, category, used);
        }

        // string.GetHashCode() differs between runs/platforms, so use a fixed FNV-1a hash instead.
        private static int StableSeed(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s) { h ^= c; h *= 16777619; }
                return (int)(h & 0x7FFFFFFF);
            }
        }
    }

    public static class ConditionAssignment
    {
        public const string Adaptive = "adaptive";
        public const string Control = "control";

        public static string Assign(string design, int participantNumber, int day)
        {
            bool oddId = participantNumber % 2 == 1;
            switch (design)
            {
                case "adaptive_only": return Adaptive;
                case "between": return oddId ? Adaptive : Control;
                default: // within_alternating
                    bool oddDay = day % 2 == 1;
                    return (oddId == oddDay) ? Adaptive : Control;
            }
        }
    }
}
