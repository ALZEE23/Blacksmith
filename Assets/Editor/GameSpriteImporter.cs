using UnityEditor;
using UnityEngine;

// Maksa semua gambar di bawah Assets/Sprites/ (QTE, HUD, dst) ke-import sebagai Sprite (2D and UI),
// biar langsung bisa dipasang ke komponen Image tanpa perlu ganti Texture Type manual satu-satu.
public class GameSpriteImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Sprites/")) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
    }
}
