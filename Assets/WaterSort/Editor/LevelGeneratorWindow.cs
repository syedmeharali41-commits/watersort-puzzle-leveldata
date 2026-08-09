#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using Designcoffers.WaterSort.Data;
using Designcoffers.WaterSort.Generator;

namespace Designcoffers.WaterSort.Editor
{
    public class LevelGeneratorWindow : EditorWindow
    {
        private int levelCount = 1000;
        private bool isGenerating = false;
        private string statusMessage = "Ready";

        [MenuItem("Designcoffers/Water Sort Level Generator")]
        public static void ShowWindow()
        {
            GetWindow<LevelGeneratorWindow>("Water Sort Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Water Sort Level Generator & Solver", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            levelCount = EditorGUILayout.IntField("Total Levels to Generate:", levelCount);

            EditorGUILayout.Space();
            if (GUILayout.Button(isGenerating ? "Generating..." : "Generate & Validate Levels", GUILayout.Height(36)))
            {
                if (!isGenerating)
                {
                    GenerateAllLevels();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        }

        private void GenerateAllLevels()
        {
            isGenerating = true;
            statusMessage = $"Generating {levelCount} solver-validated levels...";

            LevelBundle bundle = new LevelBundle();
            for (int L = 1; L <= levelCount; L++)
            {
                var lvl = WaterSortGeneratorEngine.GenerateLevel(L);
                bundle.levels.Add(lvl);

                if (L % 50 == 0)
                {
                    EditorUtility.DisplayProgressBar("Generating Levels", $"Level {L}/{levelCount}", (float)L / levelCount);
                }
            }

            EditorUtility.ClearProgressBar();

            string targetDir = Path.Combine(Application.dataPath, "Resources");
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            string outputPath = Path.Combine(targetDir, "levels.json");
            string json = GeneratorTool.SimpleJsonSerializer.SerializeBundle(bundle);
            File.WriteAllText(outputPath, json);

            AssetDatabase.Refresh();

            isGenerating = false;
            statusMessage = $"Successfully generated and saved {levelCount} levels to Assets/Resources/levels.json!";
        }
    }
}
#endif
