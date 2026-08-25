#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Game.Data;
using Game.Domain.Missions;
using Game.Domain.Resources;

namespace Game.Editor
{
    public static class StarterAssetGenerator
    {
        private const string ResourceFolder = "Assets/Game/Data/Resources";
        private const string RecipeFolder = "Assets/Game/Data/Recipes";
        private const string MissionFolder = "Assets/Game/Data/Missions";

        [MenuItem("게임/기본 경제 에셋 생성")]
        public static void Generate()
        {
            EnsureFolder(ResourceFolder);
            EnsureFolder(RecipeFolder);
            EnsureFolder(MissionFolder);

            CreateResource("iron", "철광석", 100, ResourceRarity.Common, false);
            CreateResource("coal", "석탄", 80, ResourceRarity.Common, false);
            CreateResource("wood", "목재", 60, ResourceRarity.Common, false);
            CreateResource("food", "식량", 40, ResourceRarity.Common, true);
            CreateResource("steel", "강철", 220, ResourceRarity.Uncommon, false);
            CreateResource("machinery", "기계", 600, ResourceRarity.Rare, false);
            CreateResource("medicine", "의약품", 450, ResourceRarity.Rare, true);
            CreateResource("semiconductor", "반도체", 1200, ResourceRarity.Strategic, false);
            CreateResource("horse", "말", 300, ResourceRarity.Uncommon, false);

            CreateRecipe(
                "steel_recipe",
                new[] { ("iron", 2f), ("coal", 1f) },
                new[] { ("steel", 1f) },
                10,
                5,
                "강철 생산");

            CreateRecipe(
                "horse_breeding_recipe",
                new[] { ("wood", 1f), ("food", 3f) },
                new[] { ("horse", 1f) },
                8,
                2,
                "목장 말 사육");

            CreateMission(
                "capture_iron_mine",
                MissionType.CaptureMine,
                3,
                0.25f,
                10,
                2500,
                "철광산 점령");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Starter economy assets generated.");
        }

        private static void CreateResource(
            string id,
            string displayName,
            float basePrice,
            ResourceRarity rarity,
            bool perishable)
        {
            string path = $"{ResourceFolder}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ResourceDefinitionAsset>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ResourceDefinitionAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            SetPrivateField(asset, "id", id);
            SetPrivateField(asset, "displayName", displayName);
            SetPrivateField(asset, "basePrice", basePrice);
            SetPrivateField(asset, "rarity", rarity);
            SetPrivateField(asset, "storageVolume", 1f);
            SetPrivateField(asset, "isPerishable", perishable);
            EditorUtility.SetDirty(asset);
        }

        private static void CreateRecipe(
            string id,
            (string id, float amount)[] inputs,
            (string id, float amount)[] outputs,
            float labor,
            float power,
            string displayName)
        {
            string path = $"{RecipeFolder}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<RecipeDefinitionAsset>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RecipeDefinitionAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            SetPrivateField(asset, "id", id);
            SetPrivateField(asset, "displayName", displayName);
            SetPrivateField(asset, "laborRequired", labor);
            SetPrivateField(asset, "powerRequired", power);
            SetPrivateField(asset, "daysPerCycle", 1);
            SetPrivateField(asset, "inputs", CreateAmounts(inputs));
            SetPrivateField(asset, "outputs", CreateAmounts(outputs));
            EditorUtility.SetDirty(asset);
        }

        private static void CreateMission(
            string id,
            MissionType type,
            int duration,
            float risk,
            float power,
            float reward,
            string displayName)
        {
            string path = $"{MissionFolder}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<MissionDefinitionAsset>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<MissionDefinitionAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            SetPrivateField(asset, "id", id);
            SetPrivateField(asset, "displayName", displayName);
            SetPrivateField(asset, "type", type);
            SetPrivateField(asset, "durationDays", duration);
            SetPrivateField(asset, "baseRisk", risk);
            SetPrivateField(asset, "requiredPower", power);
            SetPrivateField(asset, "reward", reward);
            EditorUtility.SetDirty(asset);
        }

        private static ResourceAmountAsset[] CreateAmounts(
            (string id, float amount)[] values)
        {
            var result = new ResourceAmountAsset[values.Length];

            for (int i = 0; i < values.Length; i++)
            {
                result[i] = new ResourceAmountAsset
                {
                    resourceId = values[i].id,
                    amount = values[i].amount
                };
            }

            return result;
        }

        private static void SetPrivateField(
            Object target,
            string fieldName,
            object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

            field?.SetValue(target, value);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
#endif
