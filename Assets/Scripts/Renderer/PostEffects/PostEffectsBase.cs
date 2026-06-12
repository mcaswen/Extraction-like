using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class PostEffectsBase : MonoBehaviour
{
    [SerializeField]
    protected Shader shader;           // 在 Inspector 指定使用的 Shader

    protected Material material;       // 自动创建的材质（不要在 Inspector 手动拖）

    protected virtual void Start()
    {
        CheckResources();
    }

    protected virtual void CheckResources()
    {
        bool isSupported = CheckSupport();

        if (!isSupported)
        {
            NotSupported();
        }
    }

    protected virtual bool CheckSupport()
    {
        return true;
    }

    protected virtual void NotSupported()
    {
        enabled = false;   // 注意：是 enabled（小写），不是 enable
        Debug.LogWarning("The image effect " + GetType() + " has been disabled due to lack of support.");
    }

    // 最常用的创建材质方法（带自动销毁保护）
    protected Material CheckShaderAndCreateMaterial(Shader targetShader, Material existingMaterial = null)
    {
        if (targetShader == null)
        {
            return null;
        }

        if (!targetShader.isSupported)
        {
            Debug.LogWarning("Shader not supported: " + targetShader.name);
            return null;
        }

        // 如果已有材质且 shader 匹配，直接复用
        if (existingMaterial != null && existingMaterial.shader == targetShader)
        {
            return existingMaterial;
        }

        // 创建新材质
        Material newMaterial = new Material(targetShader)
        {
            hideFlags = HideFlags.DontSave   // 避免保存到场景中
        };

        // 如果传入了旧材质，先销毁（防止内存泄漏）
        if (existingMaterial != null)
        {
            DestroyImmediate(existingMaterial);
        }

        return newMaterial;
    }

    // 常用重载版本：直接使用类成员 shader 和 material
    protected Material CheckShaderAndCreateMaterial()
    {
        if (material == null || material.shader != shader)
        {
            material = CheckShaderAndCreateMaterial(shader, material);
        }
        return material;
    }

    protected virtual void OnDisable()
    {
        if (material != null)
        {
            DestroyImmediate(material);
            material = null;
        }
    }
}
