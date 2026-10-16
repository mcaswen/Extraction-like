using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ExtractionLike.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class AerospaceSceneLayoutBuilder
{
    [Serializable] public sealed class LandingPolishBaseline
    {
        public string sceneHash, unrelatedHash, terrainSignature;
        public Vector3 position, scale, boundsCenter, boundsSize;
        public Quaternion rotation;
        public Vector3[] contacts;
        public string navGeometry, navPaths;
        public int agents, probes, completePaths, navVertices;
    }

    static string PolishBaselinePath => LandingPreviewFolder + "/polish-baseline.json";
    static string PolishHash(byte[] bytes)
    {
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes));
    }

    static LandingPolishBaseline ReadLandingPolishState(Scene scene)
    {
        var site = FindByName(scene, LandingRootName).GetComponent<AerospaceLandingSite>();
        TryGetBounds(site.vehicle.gameObject, out Bounds bounds);
        var nav = CaptureInvestigationNavigation(scene);
        string stableObjects = Regex.Replace(LandingOriginalSignature(scene, Array.Empty<GameObject>()), "\"instanceID\":(-?[0-9]+)", match => {
            var obj = EditorUtility.InstanceIDToObject(int.Parse(match.Groups[1].Value));
            if (obj == null) return "\"object\":\"null\"";
            var id = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            return "\"object\":\"" + (id.identifierType == 0 ? obj.GetType().FullName + "/" + obj.name : id.ToString()) + "\"";
        });
        string terrainFiles = string.Join("\n", scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true))
            .Select(t => AssetDatabase.GetAssetPath(t.terrainData)).Distinct().OrderBy(p => p)
            .Select(p => p + ":" + PolishHash(File.ReadAllBytes(p))));
        return new LandingPolishBaseline {
            sceneHash = PolishHash(File.ReadAllBytes(ScenePath)),
            unrelatedHash = PolishHash(Encoding.UTF8.GetBytes(stableObjects)),
            terrainSignature = PolishHash(Encoding.UTF8.GetBytes(terrainFiles)),
            position = site.vehicle.position, rotation = site.vehicle.rotation, scale = site.vehicle.lossyScale,
            boundsCenter = bounds.center, boundsSize = bounds.size, contacts = site.groundContactPoints,
            navGeometry = nav.GeometryHash, navPaths = PolishHash(Encoding.UTF8.GetBytes(nav.Paths)),
            agents = nav.Agents, probes = nav.Probes, completePaths = nav.Complete, navVertices = nav.Vertices
        };
    }

    public static void CaptureLandingPolishBaseline()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory(LandingPreviewFolder);
        File.WriteAllText(PolishBaselinePath, JsonUtility.ToJson(ReadLandingPolishState(scene), true));
        var site = FindByName(scene, LandingRootName).GetComponent<AerospaceLandingSite>();
        foreach (var mesh in site.vehicle.Find("LOD0").GetComponentsInChildren<MeshFilter>())
            Debug.Log("POLISH_MESH " + mesh.name + " bounds=" + mesh.sharedMesh.bounds + " localRotation=" + mesh.transform.localRotation.eulerAngles);
        Debug.Log("LANDING_POLISH_BASELINE_SAVED");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    public static void VerifyLandingPolishBaseline()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var before = JsonUtility.FromJson<LandingPolishBaseline>(File.ReadAllText(PolishBaselinePath));
        var after = ReadLandingPolishState(scene);
        File.WriteAllText(LandingPreviewFolder + "/polish-observed-state.json", JsonUtility.ToJson(after, true));
        if (before.sceneHash != after.sceneHash || before.unrelatedHash != after.unrelatedHash || before.terrainSignature != after.terrainSignature)
            throw new InvalidOperationException("Polish changed scene=" + (before.sceneHash != after.sceneHash) + ", unrelated=" + (before.unrelatedHash != after.unrelatedHash) + ", terrain=" + (before.terrainSignature != after.terrainSignature));
        if (Vector3.Distance(before.position, after.position) > .0001f || Quaternion.Angle(before.rotation, after.rotation) > .001f || Vector3.Distance(before.scale, after.scale) > .0001f)
            throw new InvalidOperationException("Vehicle placement/scale changed.");
        if (Vector3.Distance(before.boundsCenter, after.boundsCenter) > .08f || Mathf.Abs(before.boundsSize.y-after.boundsSize.y) > .01f ||
            after.boundsSize.x > before.boundsSize.x + .01f || after.boundsSize.z > before.boundsSize.z + .01f)
            throw new InvalidOperationException("Vehicle envelope changed: before=" + before.boundsCenter + "/" + before.boundsSize + " after=" + after.boundsCenter + "/" + after.boundsSize);
        if (before.contacts.Length != after.contacts.Length || before.contacts.Where((p,i) => Vector3.Distance(p,after.contacts[i]) > .0001f).Any())
            throw new InvalidOperationException("Landing ground contact moved.");
        if (before.navGeometry != after.navGeometry || before.navPaths != after.navPaths || before.agents != after.agents || before.probes != after.probes || before.completePaths != after.completePaths)
            throw new InvalidOperationException("Agent navigation changed.");
        ValidateWenchangLanding(scene);
        File.WriteAllText(LandingPreviewFolder + "/polish-verification.json", JsonUtility.ToJson(after, true));
        Debug.Log("LANDING_POLISH_VERIFIED: scene bytes, placement, scale, contacts, terrain, unrelated objects, both Agents and all navigation probes unchanged.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [MenuItem("Tools/Extraction-like/Aerospace/Wenchang Landing/Refresh Polished Vehicle Assets")]
    public static void RefreshPolishedLandingAssets()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play 模式。");
        // This routine edits the visual prefab only. It never saves or replaces a scene.
        var prefab = PrefabUtility.LoadPrefabContents(LandingPrefab);
        try
        {
            var site = prefab.GetComponent<AerospaceLandingSite>();
            foreach (var renderer in site.vehicle.GetComponentsInChildren<MeshRenderer>(true))
                renderer.sharedMaterial = LandingVehicleMaterial(renderer.name.Split('_')[1]);
            site.vehicleLods.RecalculateBounds();
            for (int i = 0; i < site.smoke.Length; i++) ConfigureLandingSmoke(site.smoke[i], i);
            for (int i = 0; i < site.smokeWisps.Length; i++)
            {
                var old = site.smokeWisps[i];
                var generated = CreateLandingSmokeWisp(old.transform.parent, site.smoke[i].transform.position, i);
                // StoreLandingMesh updates the existing mesh asset; retain the original object IDs.
                old.sharedMaterial = generated.sharedMaterial;
                old.transform.localScale = i == 0 ? Vector3.one : Vector3.one * .72f;
                UnityEngine.Object.DestroyImmediate(generated.gameObject);
            }
            site.ApplyPhase(0);
            SetSurveyVisualOnly(prefab);
            ValidateInvestigationVisualRoot(prefab);
            if (PrefabUtility.SaveAsPrefabAsset(prefab, LandingPrefab) == null)
                throw new IOException("Could not save polished landing prefab.");
            AssetDatabase.SaveAssets();
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        Debug.Log("LANDING_POLISH_ASSETS_REFRESHED; no scene saved.");
    }

    public static void RefreshVerifyAndRenderLandingPolish()
    {
        RefreshPolishedLandingAssets(); VerifyLandingPolishBaseline(); RenderWenchangLanding();
    }
}
