// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine.Rendering;
// using UnityEngine.Rendering.Universal;
// using UnityEngine;

// [System.Serializale]
// [VolumeComponentMenuForRenderPipeline("Custom/VolumetricCloud",typeof(UniversalRenderPipeline))]
// public class VolumetricCloud : VolumeComponent
// {
//     IPostProcessComponent()
//     {
//         [Tooltip("BaseColor")]
//         ColorParameter baseColor = new ColorParameter(new Color(1,1,1,1));

//         public bool IsActive() => true;
//         public bool IsTileCompatible() =>false;
//         public void load(Material material,refRenderingData data)
//         {
//             material.SetColor("_BaseColor",baseColor.value);
//         }
//     }
// }
