#if UNITY_EDITOR
using Gameplay.Agent.Combat;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成智能体默认战斗技能配置、材质、网格和特效预制体的编辑器工具
/// </summary>
public static class AgentCombatContentGenerator
{
    private const string ResourceRoot = "Assets/Resources/Agent/Combat";
    private const string SkillRoot = ResourceRoot + "/Skills";
    private const string MaterialRoot = ResourceRoot + "/Materials";
    private const string MeshRoot = ResourceRoot + "/Meshes";
    private const string PrefabRoot = "Assets/Prefabs/Agent/Combat/VFX";

    [InitializeOnLoadMethod]
    private static void AutoGenerateMissingContent()
    {
        // 延迟到编辑器初始化完成后再检查资源，避免资源数据库尚未可用
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (HasGeneratedContent())
                return;

            GenerateDefaultCombatContent();
        };
    }

    /// <summary>
    /// 生成或刷新智能体默认战斗内容资源
    /// </summary>
    [MenuItem("Tools/Agent/Combat/Generate Default Combat Content")]
    public static void GenerateDefaultCombatContent()
    {
        EnsureFolder("Assets/Resources", "Agent");
        EnsureFolder("Assets/Resources/Agent", "Combat");
        EnsureFolder(ResourceRoot, "Skills");
        EnsureFolder(ResourceRoot, "Materials");
        EnsureFolder(ResourceRoot, "Meshes");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "Agent");
        EnsureFolder("Assets/Prefabs/Agent", "Combat");
        EnsureFolder("Assets/Prefabs/Agent/Combat", "VFX");

        Material iceAreaMaterial = CreateMaterial(
            MaterialRoot + "/MAT_Agent_Ice_Area.mat",
            new Color(0.46f, 0.9f, 1f, 0.45f));
        Material iceSolidMaterial = CreateMaterial(
            MaterialRoot + "/MAT_Agent_Ice_Solid.mat",
            new Color(0.78f, 0.96f, 1f, 1f));
        Material earthAreaMaterial = CreateMaterial(
            MaterialRoot + "/MAT_Agent_Earth_Area.mat",
            new Color(0.55f, 0.39f, 0.23f, 0.55f));
        Material earthWallMaterial = CreateMaterial(
            MaterialRoot + "/MAT_Agent_Earth_Wall.mat",
            new Color(0.43f, 0.33f, 0.24f, 1f));

        Mesh frostConeMesh = CreateMeshAsset(
            MeshRoot + "/M_Agent_FrostAssault_Cone.asset",
            BuildConeMesh(8f, 120f, 32));
        Mesh circleMesh = CreateMeshAsset(
            MeshRoot + "/M_Agent_Combat_Circle.asset",
            BuildDiscMesh(4f, 48));

        GameObject frostAssaultPrefab = CreateFrostAssaultPrefab(frostConeMesh, iceAreaMaterial, iceSolidMaterial);
        GameObject winterfallPrefab = CreateWinterfallPrefab(circleMesh, iceAreaMaterial, iceSolidMaterial);
        GameObject stoneWallPrefab = CreateStoneWallPrefab(earthWallMaterial);
        GameObject quakeFieldPrefab = CreateQuakeFieldPrefab(circleMesh, earthAreaMaterial, earthWallMaterial);

        AgentConeDamageSkillConfig frostAssaultSkill = CreateOrLoadAsset<AgentConeDamageSkillConfig>(
            SkillRoot + "/SO_AgentSkill_Ice_FrostAssault.asset");
        ConfigureSkillBase(frostAssaultSkill, "ice_frost_assault", "霜袭", AgentCombatElementType.Ice, 9f);
        ConfigureConeSkill(
            frostAssaultSkill,
            AgentCombatStatScalingSource.Attack,
            2.5f,
            8f,
            120f,
            true,
            0.5f,
            3f,
            frostAssaultPrefab,
            new Color(0.46f, 0.9f, 1f, 0.45f),
            0.45f);

        AgentAreaDamageSkillConfig winterfallSkill = CreateOrLoadAsset<AgentAreaDamageSkillConfig>(
            SkillRoot + "/SO_AgentSkill_Ice_Winterfall.asset");
        ConfigureSkillBase(winterfallSkill, "ice_winterfall", "凛冬", AgentCombatElementType.Ice, 12f);
        ConfigureAreaSkill(
            winterfallSkill,
            AgentCombatStatScalingSource.Attack,
            3f,
            4f,
            winterfallPrefab,
            new Color(0.72f, 0.95f, 1f, 0.5f),
            0.65f);

        AgentWallSkillConfig stoneWallSkill = CreateOrLoadAsset<AgentWallSkillConfig>(
            SkillRoot + "/SO_AgentSkill_Earth_StoneWall.asset");
        ConfigureSkillBase(stoneWallSkill, "earth_stone_wall", "磐石", AgentCombatElementType.Earth, 12f);
        ConfigureWallSkill(
            stoneWallSkill,
            AgentCombatStatScalingSource.Defense,
            0.8f,
            4f,
            0.65f,
            2.4f,
            1.8f,
            10f,
            stoneWallPrefab,
            new Color(0.43f, 0.33f, 0.24f, 1f));

        AgentAreaDamageOverTimeSkillConfig quakeFieldSkill = CreateOrLoadAsset<AgentAreaDamageOverTimeSkillConfig>(
            SkillRoot + "/SO_AgentSkill_Earth_QuakeField.asset");
        ConfigureSkillBase(quakeFieldSkill, "earth_quake_field", "撼动", AgentCombatElementType.Earth, 15f);
        ConfigureAreaOverTimeSkill(
            quakeFieldSkill,
            AgentCombatStatScalingSource.Defense,
            0.5f,
            4f,
            5f,
            1f,
            quakeFieldPrefab,
            new Color(0.55f, 0.39f, 0.23f, 0.55f));

        AgentCombatStyleConfig iceStyle = CreateOrLoadAsset<AgentCombatStyleConfig>(
            ResourceRoot + "/SO_AgentCombatStyle_Ice.asset");
        ConfigureStyle(
            iceStyle,
            "ice",
            "冰",
            AgentCombatElementType.Ice,
            AgentCombatWeaponType.Catalyst,
            8f,
            0.65f,
            AgentCombatStatScalingSource.Attack,
            1f,
            frostAssaultSkill,
            winterfallSkill);

        AgentCombatStyleConfig earthStyle = CreateOrLoadAsset<AgentCombatStyleConfig>(
            ResourceRoot + "/SO_AgentCombatStyle_Earth.asset");
        ConfigureStyle(
            earthStyle,
            "earth",
            "土",
            AgentCombatElementType.Earth,
            AgentCombatWeaponType.Sword,
            2.4f,
            0.8f,
            AgentCombatStatScalingSource.Defense,
            0.9f,
            stoneWallSkill,
            quakeFieldSkill);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Generated default Agent combat styles, skills and VFX prefabs.");
    }

    private static bool HasGeneratedContent()
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/PF_Agent_Ice_FrostAssault.prefab") != null &&
               AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/PF_Agent_Ice_Winterfall.prefab") != null &&
               AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/PF_Agent_Earth_StoneWall.prefab") != null &&
               AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/PF_Agent_Earth_QuakeField.prefab") != null &&
               AssetDatabase.LoadAssetAtPath<AgentCombatStyleConfig>(ResourceRoot + "/SO_AgentCombatStyle_Ice.asset") != null &&
               AssetDatabase.LoadAssetAtPath<AgentCombatStyleConfig>(ResourceRoot + "/SO_AgentCombatStyle_Earth.asset") != null;
    }

    private static GameObject CreateFrostAssaultPrefab(
        Mesh coneMesh,
        Material areaMaterial,
        Material solidMaterial)
    {
        GameObject root = new GameObject("PF_Agent_Ice_FrostAssault");
        CreateMeshChild(root.transform, "Cone", coneMesh, areaMaterial, Vector3.zero, Quaternion.identity, Vector3.one);

        for (int i = 0; i < 9; i++)
        {
            float t = i / 8f;
            float angle = Mathf.Lerp(-52f, 52f, t) * Mathf.Deg2Rad;
            float distance = Mathf.Lerp(2.1f, 7.3f, (i % 3 + 1) / 3f);
            Vector3 localPosition = new Vector3(Mathf.Sin(angle) * distance, 0.38f, Mathf.Cos(angle) * distance);
            GameObject spike = CreatePrimitiveChild(
                root.transform,
                "IceSpike",
                PrimitiveType.Cylinder,
                solidMaterial,
                localPosition,
                Quaternion.Euler(0f, Mathf.Rad2Deg * angle, 0f),
                new Vector3(0.12f, Mathf.Lerp(0.55f, 1.1f, t), 0.12f));
            DisableCollider(spike);
        }

        return SavePrefab(root, PrefabRoot + "/PF_Agent_Ice_FrostAssault.prefab");
    }

    private static GameObject CreateWinterfallPrefab(
        Mesh circleMesh,
        Material areaMaterial,
        Material solidMaterial)
    {
        GameObject root = new GameObject("PF_Agent_Ice_Winterfall");
        CreateMeshChild(root.transform, "ImpactCircle", circleMesh, areaMaterial, Vector3.zero, Quaternion.identity, Vector3.one);

        for (int i = 0; i < 10; i++)
        {
            float angle = Mathf.PI * 2f * i / 10f;
            float radius = i % 2 == 0 ? 2.6f : 3.35f;
            Vector3 localPosition = new Vector3(Mathf.Cos(angle) * radius, 0.7f, Mathf.Sin(angle) * radius);
            GameObject pillar = CreatePrimitiveChild(
                root.transform,
                "IcePillar",
                PrimitiveType.Cube,
                solidMaterial,
                localPosition,
                Quaternion.Euler(0f, -Mathf.Rad2Deg * angle, 0f),
                new Vector3(0.18f, 1.35f + (i % 3) * 0.25f, 0.18f));
            DisableCollider(pillar);
        }

        return SavePrefab(root, PrefabRoot + "/PF_Agent_Ice_Winterfall.prefab");
    }

    private static GameObject CreateStoneWallPrefab(Material wallMaterial)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        root.name = "PF_Agent_Earth_StoneWall";
        root.transform.localScale = Vector3.one;
        ApplyMaterial(root, wallMaterial);

        Rigidbody rigidbodyComponent = root.AddComponent<Rigidbody>();
        rigidbodyComponent.isKinematic = true;
        rigidbodyComponent.useGravity = false;

        for (int i = 0; i < 6; i++)
        {
            float x = Mathf.Lerp(-0.42f, 0.42f, i / 5f);
            GameObject ridge = CreatePrimitiveChild(
                root.transform,
                "StoneRidge",
                PrimitiveType.Cube,
                wallMaterial,
                new Vector3(x, 0.05f * (i % 2), -0.54f),
                Quaternion.Euler(0f, 0f, i % 2 == 0 ? 8f : -7f),
                new Vector3(0.08f, 0.8f, 0.08f));
            DisableCollider(ridge);
        }

        return SavePrefab(root, PrefabRoot + "/PF_Agent_Earth_StoneWall.prefab");
    }

    private static GameObject CreateQuakeFieldPrefab(
        Mesh circleMesh,
        Material areaMaterial,
        Material crackMaterial)
    {
        GameObject root = new GameObject("PF_Agent_Earth_QuakeField");
        CreateMeshChild(root.transform, "QuakeCircle", circleMesh, areaMaterial, Vector3.zero, Quaternion.identity, Vector3.one);

        for (int i = 0; i < 8; i++)
        {
            float angle = Mathf.PI * 2f * i / 8f;
            float length = 1.8f + (i % 3) * 0.45f;
            Vector3 localPosition = new Vector3(Mathf.Cos(angle) * 1.2f, 0.035f, Mathf.Sin(angle) * 1.2f);
            GameObject crack = CreatePrimitiveChild(
                root.transform,
                "EarthCrack",
                PrimitiveType.Cube,
                crackMaterial,
                localPosition,
                Quaternion.Euler(0f, -Mathf.Rad2Deg * angle + 90f, 0f),
                new Vector3(length, 0.035f, 0.08f));
            DisableCollider(crack);
        }

        return SavePrefab(root, PrefabRoot + "/PF_Agent_Earth_QuakeField.prefab");
    }

    private static GameObject CreateMeshChild(
        Transform parent,
        string name,
        Mesh mesh,
        Material material,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = localRotation;
        child.transform.localScale = localScale;

        MeshFilter meshFilter = child.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();
        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        return child;
    }

    private static GameObject CreatePrimitiveChild(
        Transform parent,
        string name,
        PrimitiveType primitiveType,
        Material material,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale)
    {
        GameObject child = GameObject.CreatePrimitive(primitiveType);
        child.name = name;
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = localRotation;
        child.transform.localScale = localScale;
        ApplyMaterial(child, material);
        return child;
    }

    private static void ConfigureSkillBase(
        AgentCombatSkillConfigBase skill,
        string skillId,
        string displayName,
        AgentCombatElementType element,
        float cooldownSeconds)
    {
        SerializedObject serializedObject = new SerializedObject(skill);
        serializedObject.FindProperty("_skillId").stringValue = skillId;
        serializedObject.FindProperty("_displayName").stringValue = displayName;
        serializedObject.FindProperty("_element").enumValueIndex = (int)element;
        serializedObject.FindProperty("_cooldownSeconds").floatValue = cooldownSeconds;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
    }

    private static void ConfigureConeSkill(
        AgentConeDamageSkillConfig skill,
        AgentCombatStatScalingSource damageSource,
        float damageMultiplier,
        float radius,
        float angleDegrees,
        bool applySlow,
        float slowMultiplier,
        float slowDuration,
        GameObject visualPrefab,
        Color indicatorColor,
        float indicatorDuration)
    {
        SerializedObject serializedObject = new SerializedObject(skill);
        SetScaling(serializedObject.FindProperty("_damage"), damageSource, damageMultiplier);
        serializedObject.FindProperty("_radius").floatValue = radius;
        serializedObject.FindProperty("_angleDegrees").floatValue = angleDegrees;
        SetStatus(serializedObject.FindProperty("_statusEffect"), applySlow, slowMultiplier, slowDuration, false, 0f);
        serializedObject.FindProperty("_visualPrefab").objectReferenceValue = visualPrefab;
        serializedObject.FindProperty("_indicatorColor").colorValue = indicatorColor;
        serializedObject.FindProperty("_indicatorDuration").floatValue = indicatorDuration;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
    }

    private static void ConfigureAreaSkill(
        AgentAreaDamageSkillConfig skill,
        AgentCombatStatScalingSource damageSource,
        float damageMultiplier,
        float radius,
        GameObject visualPrefab,
        Color indicatorColor,
        float indicatorDuration)
    {
        SerializedObject serializedObject = new SerializedObject(skill);
        SetScaling(serializedObject.FindProperty("_damage"), damageSource, damageMultiplier);
        serializedObject.FindProperty("_radius").floatValue = radius;
        serializedObject.FindProperty("_visualPrefab").objectReferenceValue = visualPrefab;
        serializedObject.FindProperty("_indicatorColor").colorValue = indicatorColor;
        serializedObject.FindProperty("_indicatorDuration").floatValue = indicatorDuration;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
    }

    private static void ConfigureWallSkill(
        AgentWallSkillConfig skill,
        AgentCombatStatScalingSource damageSource,
        float damageMultiplier,
        float length,
        float width,
        float height,
        float forwardDistance,
        float durationSeconds,
        GameObject wallPrefab,
        Color wallColor)
    {
        SerializedObject serializedObject = new SerializedObject(skill);
        SetScaling(serializedObject.FindProperty("_damage"), damageSource, damageMultiplier);
        serializedObject.FindProperty("_length").floatValue = length;
        serializedObject.FindProperty("_width").floatValue = width;
        serializedObject.FindProperty("_height").floatValue = height;
        serializedObject.FindProperty("_forwardDistance").floatValue = forwardDistance;
        serializedObject.FindProperty("_durationSeconds").floatValue = durationSeconds;
        serializedObject.FindProperty("_wallPrefab").objectReferenceValue = wallPrefab;
        serializedObject.FindProperty("_wallColor").colorValue = wallColor;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
    }

    private static void ConfigureAreaOverTimeSkill(
        AgentAreaDamageOverTimeSkillConfig skill,
        AgentCombatStatScalingSource damageSource,
        float damageMultiplier,
        float radius,
        float durationSeconds,
        float tickInterval,
        GameObject visualPrefab,
        Color indicatorColor)
    {
        SerializedObject serializedObject = new SerializedObject(skill);
        SetScaling(serializedObject.FindProperty("_damagePerSecond"), damageSource, damageMultiplier);
        serializedObject.FindProperty("_radius").floatValue = radius;
        serializedObject.FindProperty("_durationSeconds").floatValue = durationSeconds;
        serializedObject.FindProperty("_tickInterval").floatValue = tickInterval;
        serializedObject.FindProperty("_visualPrefab").objectReferenceValue = visualPrefab;
        serializedObject.FindProperty("_indicatorColor").colorValue = indicatorColor;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
    }

    private static void ConfigureStyle(
        AgentCombatStyleConfig style,
        string styleId,
        string displayName,
        AgentCombatElementType element,
        AgentCombatWeaponType weaponType,
        float normalAttackRange,
        float normalAttackInterval,
        AgentCombatStatScalingSource normalAttackSource,
        float normalAttackMultiplier,
        params AgentCombatSkillConfigBase[] skills)
    {
        SerializedObject serializedObject = new SerializedObject(style);
        serializedObject.FindProperty("_styleId").stringValue = styleId;
        serializedObject.FindProperty("_displayName").stringValue = displayName;
        serializedObject.FindProperty("_element").enumValueIndex = (int)element;
        serializedObject.FindProperty("_weaponType").enumValueIndex = (int)weaponType;
        serializedObject.FindProperty("_normalAttackRange").floatValue = normalAttackRange;
        serializedObject.FindProperty("_normalAttackInterval").floatValue = normalAttackInterval;
        SetScaling(serializedObject.FindProperty("_normalAttackDamage"), normalAttackSource, normalAttackMultiplier);

        SerializedProperty skillsProperty = serializedObject.FindProperty("_skills");
        skillsProperty.arraySize = skills.Length;
        for (int i = 0; i < skills.Length; i++)
            skillsProperty.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(style);
    }

    private static void SetScaling(
        SerializedProperty property,
        AgentCombatStatScalingSource source,
        float multiplier)
    {
        property.FindPropertyRelative("_source").enumValueIndex = (int)source;
        property.FindPropertyRelative("_multiplier").floatValue = multiplier;
    }

    private static void SetStatus(
        SerializedProperty property,
        bool applySlow,
        float slowMultiplier,
        float slowDuration,
        bool applyFreeze,
        float freezeDuration)
    {
        property.FindPropertyRelative("_applySlow").boolValue = applySlow;
        property.FindPropertyRelative("_slowMultiplier").floatValue = slowMultiplier;
        property.FindPropertyRelative("_slowDurationSeconds").floatValue = slowDuration;
        property.FindPropertyRelative("_applyFreeze").boolValue = applyFreeze;
        property.FindPropertyRelative("_freezeDurationSeconds").floatValue = freezeDuration;
    }

    private static T CreateOrLoadAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static Material CreateMaterial(string path, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh CreateMeshAsset(string path, Mesh mesh)
    {
        Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existingMesh != null)
        {
            EditorUtility.CopySerialized(mesh, existingMesh);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existingMesh);
            return existingMesh;
        }

        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static GameObject SavePrefab(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static Mesh BuildConeMesh(float radius, float angleDegrees, int segments)
    {
        int safeSegments = Mathf.Clamp(segments, 4, 64);
        Vector3[] vertices = new Vector3[safeSegments + 2];
        int[] triangles = new int[safeSegments * 3];

        vertices[0] = Vector3.zero;
        float halfAngle = angleDegrees * 0.5f;
        for (int i = 0; i <= safeSegments; i++)
        {
            float t = i / (float)safeSegments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
        }

        for (int i = 0; i < safeSegments; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i + 2;
        }

        Mesh mesh = new Mesh();
        mesh.name = "AgentCombatConeMesh";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Mesh BuildDiscMesh(float radius, int segments)
    {
        int safeSegments = Mathf.Clamp(segments, 8, 96);
        Vector3[] vertices = new Vector3[safeSegments + 1];
        int[] triangles = new int[safeSegments * 3];
        vertices[0] = Vector3.zero;

        for (int i = 0; i < safeSegments; i++)
        {
            float angle = Mathf.PI * 2f * i / safeSegments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        for (int i = 0; i < safeSegments; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i == safeSegments - 1 ? 1 : i + 2;
        }

        Mesh mesh = new Mesh();
        mesh.name = "AgentCombatDiscMesh";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    private static void ApplyMaterial(GameObject target, Material material)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    private static void DisableCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);
    }

    private static void EnsureFolder(string parent, string folder)
    {
        string path = parent + "/" + folder;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folder);
    }
}
#endif
