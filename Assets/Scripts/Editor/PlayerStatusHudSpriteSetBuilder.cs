using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成玩家状态界面图片配置资产的编辑器工具
/// </summary>
public static class PlayerStatusHudSpriteSetBuilder
{
    private const string SpriteSetPath = "Assets/Resources/HUD/PlayerStatusHudSpriteSet.asset";
    private static readonly Rect AvatarIconCropRect = new Rect(489f, 497f, 478f, 478f);

    private const string AvatarIconPath = "Assets/Art/Sprites/UI/Main_character, progress_bar, status_bar/IMG_0582.PNG";
    private const string HealthIconPath = "Assets/Art/Sprites/UI/Main_character, progress_bar, status_bar/IMG_0584.PNG";
    private const string CarryIconPath = "Assets/Art/Sprites/UI/Main_character, progress_bar, status_bar/IMG_0586.PNG";
    private const string ProgressBarPath = "Assets/Art/Sprites/UI/Main_character, progress_bar, status_bar/IMG_0587.PNG";

    /// <summary>
    /// 重建玩家状态界面图片集合资产
    /// </summary>
    [MenuItem("Tools/Raid/Rebuild Player Status HUD Sprite Set")]
    public static void Rebuild()
    {
        Sprite avatarIcon = LoadSprite(AvatarIconPath);
        Sprite healthIcon = LoadSprite(HealthIconPath);
        Sprite carryIcon = LoadSprite(CarryIconPath);
        Sprite progressBar = LoadSprite(ProgressBarPath);

        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "HUD");

        PlayerStatusHudSpriteSet spriteSet = AssetDatabase.LoadAssetAtPath<PlayerStatusHudSpriteSet>(SpriteSetPath);
        if (spriteSet == null)
        {
            spriteSet = ScriptableObject.CreateInstance<PlayerStatusHudSpriteSet>();
            AssetDatabase.CreateAsset(spriteSet, SpriteSetPath);
        }

        spriteSet.Configure(avatarIcon, healthIcon, carryIcon, progressBar, AvatarIconCropRect);
        EditorUtility.SetDirty(spriteSet);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Rebuilt player status HUD sprite set at {SpriteSetPath}");
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            throw new FileNotFoundException($"Missing status HUD sprite at {path}", path);
        }

        return sprite;
    }

    private static void EnsureFolder(string parent, string folderName)
    {
        string path = $"{parent}/{folderName}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
