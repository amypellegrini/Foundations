using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Turns the rigged Quaternius characters (CC0, "Ultimate Animated Character
/// Pack") into the Hunter and Founder unit prefabs: looping clips, editable
/// material assets with proper skin tones, one shared locomotion controller
/// whose blend thresholds are measured from the clips themselves, and the
/// props carried over from the hand-built miniatures.
/// </summary>
public static class CharacterUnitsBuilder
{
    const string CharacterDir = "Assets/Models/Characters";
    const string MaterialDir = CharacterDir + "/Materials";
    const string ControllerPath = CharacterDir + "/UnitCharacter.controller";
    const string PrefabDir = "Assets/Foundations3D/Prefabs";
    const string MiniatureDir = "Assets/Foundations3D/Prefabs";

    const string HunterModel = "Elf";
    const string FounderModel = "Viking_Male";

    /// <summary>World height of a character relative to the 2.8-unit source rig.</summary>
    const float CharacterScale = 0.55f;

    static readonly string[] LoopingClips = { "Idle", "Walk", "Run", "Walk_Carry", "Run_Carry" };

    // Per-model skin tone; the pack ships its skin as near-black grey.
    static readonly Dictionary<string, string> SkinTones = new Dictionary<string, string>
    {
        { HunterModel, "C8956F" },
        { FounderModel, "E0B08E" },
    };

    [MenuItem("Foundations/Upgrade Units To Animated Characters")]
    public static void BuildAndApply()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Stop Play Mode before rebuilding.");
        var prefabs = BuildPrefabs();
        ApplyToScene(prefabs.hunter, prefabs.founder);
    }

    /// <summary>Builds (or rebuilds) both character prefabs and returns them.</summary>
    public static (GameObject hunter, GameObject founder) BuildPrefabs()
    {
        Directory.CreateDirectory(MaterialDir);
        AssetDatabase.Refresh();

        ConfigureModel(HunterModel);
        ConfigureModel(FounderModel);

        var clips = LoadClips(ModelPath(HunterModel));
        float walkSpeed = MeasureGroundSpeed(HunterModel, clips["Walk"]) * CharacterScale;
        float runSpeed = MeasureGroundSpeed(HunterModel, clips["Run"]) * CharacterScale;
        var controller = BuildController(clips, walkSpeed, runSpeed);

        var hunterMini = AssetDatabase.LoadAssetAtPath<GameObject>(MiniatureDir + "/Hunter miniature.prefab");
        var founderMini = AssetDatabase.LoadAssetAtPath<GameObject>(MiniatureDir + "/Founder miniature.prefab");

        var hunter = BuildCharacter("Hunter character", HunterModel, controller, clips, walkSpeed, runSpeed,
            hunterMini, new[]
            {
                new Prop("Bow", "Fist.L", new Vector3(-.43f, .905f, .49f), n => n.StartsWith("Bow")),
                new Prop("Quiver", "Torso", null, n => n == "Quiver" || n == "Arrow" || n == "Fletching"),
            });
        var founder = BuildCharacter("Founder character", FounderModel, controller, clips, walkSpeed, runSpeed,
            founderMini, new[]
            {
                // Gripped ~0.49 up the staff so its foot rests on the ground at the fist's idle height.
                new Prop("Staff", "Fist.L", new Vector3(-.43f, .52f, .29f), n => n == "Walking staff"),
                new Prop("Pack", "Torso", null, n => n == "Travelling pack"),
            });

        AssetDatabase.SaveAssets();
        Debug.Log($"Character units built: walk cycle {walkSpeed:F2} u/s, run cycle {runSpeed:F2} u/s.");
        return (hunter, founder);
    }

    /// <summary>Swaps the miniature on each scene unit for its character prefab.</summary>
    public static void ApplyToScene(GameObject hunterPrefab, GameObject founderPrefab)
    {
        foreach (var unit in UnityEngine.Object.FindObjectsByType<Unit>())
        {
            AttachCharacter(unit, unit.canFoundCity ? founderPrefab : hunterPrefab);
            EditorUtility.SetDirty(unit);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    /// <summary>
    /// Replaces any existing visual on the unit with a character instance and
    /// sets the movement tuning the character's stride was measured against.
    /// </summary>
    public static void AttachCharacter(Unit unit, GameObject prefab)
    {
        foreach (Transform child in new List<Transform>(unit.transform.Cast()))
        {
            if (unit.nameLabel != null && unit.nameLabel.transform.IsChildOf(child)) continue;
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, unit.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.GetComponent<UnitCharacterAnimator>().unit = unit;

        unit.moveSpeed = 2.2f;
        unit.acceleration = 4f;
        unit.deceleration = 3f;
        unit.turnSpeed = 360f;
        unit.turnInPlaceAngle = 35f;
        unit.cornerRounding = 0.85f;
    }

    static IEnumerable<Transform> Cast(this Transform t)
    {
        foreach (Transform c in t) yield return c;
    }

    static string ModelPath(string model) => $"{CharacterDir}/{model}.fbx";

    /// <summary>Loops the locomotion clips and remaps materials to editable assets.</summary>
    static void ConfigureModel(string model)
    {
        string path = ModelPath(model);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);

        var clipSettings = importer.defaultClipAnimations;
        foreach (var clip in clipSettings)
            clip.loopTime = Array.IndexOf(LoopingClips, ShortName(clip.name)) >= 0;
        importer.clipAnimations = clipSettings;
        importer.SaveAndReimport();

        foreach (var embedded in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (!(embedded is Material source)) continue;
            string matPath = $"{MaterialDir}/{model}_{source.name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(source);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetFloat("_Smoothness", .12f);
            if (source.name == "Skin" && SkinTones.TryGetValue(model, out string hex) &&
                ColorUtility.TryParseHtmlString("#" + hex, out Color skin))
                mat.SetColor("_BaseColor", skin);
            EditorUtility.SetDirty(mat);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), mat);
        }
        importer.SaveAndReimport();
    }

    static string ShortName(string clipName)
    {
        int bar = clipName.LastIndexOf('|');
        return bar >= 0 ? clipName.Substring(bar + 1) : clipName;
    }

    static Dictionary<string, AnimationClip> LoadClips(string path)
    {
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is AnimationClip c && !c.name.StartsWith("__preview"))
                clips[ShortName(c.name)] = c;
        return clips;
    }

    /// <summary>
    /// Estimates the ground speed a clip implies, at the source rig's scale:
    /// how fast a planted foot sweeps backward relative to the body. Feet are
    /// treated as planted when they're within a few centimeters of their
    /// lowest point. Moving the character at this speed keeps feet from sliding.
    /// </summary>
    static float MeasureGroundSpeed(string model, AnimationClip clip)
    {
        var go = (GameObject)UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(model)));
        try
        {
            const int N = 120;
            float total = 0f;
            int count = 0;
            foreach (string bone in new[] { "LowerLeg.L_end", "LowerLeg.R_end" })
            {
                Transform foot = FindDeep(go.transform, bone);
                var pos = new Vector3[N + 1];
                float minY = float.MaxValue;
                for (int i = 0; i <= N; i++)
                {
                    clip.SampleAnimation(go, clip.length * i / N);
                    pos[i] = go.transform.InverseTransformPoint(foot.position);
                    minY = Mathf.Min(minY, pos[i].y);
                }
                float dt = clip.length / N;
                for (int i = 0; i < N; i++)
                {
                    if (pos[i].y > minY + 0.08f || pos[i + 1].y > minY + 0.08f) continue;
                    float v = -(pos[i + 1].z - pos[i].z) / dt;
                    if (v <= 0f) continue;
                    total += v;
                    count++;
                }
            }
            return count > 0 ? total / count : 1f;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static AnimatorController BuildController(Dictionary<string, AnimationClip> clips, float walkSpeed, float runSpeed)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
            AssetDatabase.DeleteAsset(ControllerPath);
        controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        controller.CreateBlendTreeInController("Locomotion", out BlendTree tree);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(clips["Idle"], 0f);
        tree.AddChild(clips["Walk"], walkSpeed);
        tree.AddChild(clips["Run"], runSpeed);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    readonly struct Prop
    {
        public readonly string name, bone;
        public readonly Vector3? grip;
        public readonly Func<string, bool> include;

        /// <param name="grip">Point in miniature space that should sit on the
        /// bone (e.g. a bow's handle in the fist). Null keeps the prop where it
        /// was on the miniature and just rides on the bone.</param>
        public Prop(string name, string bone, Vector3? grip, Func<string, bool> include)
        {
            this.name = name; this.bone = bone; this.grip = grip; this.include = include;
        }
    }

    static GameObject BuildCharacter(string name, string model, AnimatorController controller,
        Dictionary<string, AnimationClip> clips, float walkSpeed, float runSpeed,
        GameObject miniature, Prop[] props)
    {
        var root = new GameObject(name);
        var body = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(model)), root.transform);
        body.transform.localScale = Vector3.one * CharacterScale;

        var animator = body.GetComponent<Animator>();
        if (animator == null) animator = body.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        // Fit props against the standing pose the character will mostly be seen in.
        clips["Idle"].SampleAnimation(body, 0f);

        if (miniature != null)
        {
            var mini = (GameObject)PrefabUtility.InstantiatePrefab(miniature);
            PrefabUtility.UnpackPrefabInstance(mini, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            mini.transform.SetPositionAndRotation(root.transform.position, root.transform.rotation);
            foreach (var prop in props)
            {
                var pivot = new GameObject(prop.name).transform;
                pivot.SetParent(mini.transform, false);
                pivot.localPosition = prop.grip ?? Vector3.zero;
                foreach (Transform part in new List<Transform>(mini.transform.Cast()))
                    if (part != pivot && prop.include(part.name))
                        part.SetParent(pivot, true);

                Transform bone = FindDeep(body.transform, prop.bone);
                if (prop.grip.HasValue)
                    pivot.position = bone.position;
                pivot.SetParent(bone, true);
            }
            UnityEngine.Object.DestroyImmediate(mini);
        }

        var driver = root.AddComponent<UnitCharacterAnimator>();
        driver.animator = animator;
        driver.walkCycleSpeed = walkSpeed;
        driver.runCycleSpeed = runSpeed;

        string path = $"{PrefabDir}/{name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var found = FindDeep(c, name);
            if (found != null) return found;
        }
        return null;
    }
}
