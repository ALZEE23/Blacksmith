using UnityEditor;
using UnityEngine;

// Maksa semua gambar di Assets/Sprites/QTE ke-import sebagai Sprite (2D and UI), biar langsung
// bisa dipasang ke komponen Image tanpa perlu ganti Texture Type manual satu-satu.
public class QteSpriteImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Sprites/QTE/")) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
    }
}
