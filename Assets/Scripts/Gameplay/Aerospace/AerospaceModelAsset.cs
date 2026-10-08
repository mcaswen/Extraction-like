using UnityEngine;
namespace ExtractionLike.Aerospace
{
    public sealed class AerospaceModelAsset : MonoBehaviour
    {
        public Renderer[] meshes;
        public Material[] cleanMaterials;
        public Material[] recoveredMaterials;
        public void ApplySurface(bool recovered)
        {
            var materials = recovered ? recoveredMaterials : cleanMaterials;
            for (int i = 0; i < meshes.Length; i++) if (meshes[i] != null && i < materials.Length) meshes[i].sharedMaterial = materials[i];
        }
    }
}
