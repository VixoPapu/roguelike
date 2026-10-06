#if UNITY_EDITOR
using System.Linq;
using ProjectLike.Phase12;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PowerUpData))]
public class PowerUpDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var data = (PowerUpData)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("PowerUpIcons.png usa celdas de 32x32. Fila 0 es la fila superior y columna 0 la izquierda. También puedes arrastrar directamente un sprite al campo Icon.", MessageType.Info);
        if (GUILayout.Button("Aplicar columna/fila del sheet")) ApplyCoordinates(data);
        if (data.icon) GUILayout.Label(AssetPreview.GetAssetPreview(data.icon), GUILayout.Width(96), GUILayout.Height(96));
    }

    static void ApplyCoordinates(PowerUpData data)
    {
        if (!data.iconSheet) { EditorUtility.DisplayDialog("Power-up", "Asigna PowerUpIcons.png en Icon Sheet.", "OK"); return; }
        var path = AssetDatabase.GetAssetPath(data.iconSheet);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>();
        var expectedX = data.iconColumn * 32;
        var expectedY = data.iconSheet.height - (data.iconRow + 1) * 32;
        var sprite = sprites.FirstOrDefault(candidate => Mathf.RoundToInt(candidate.rect.x) == expectedX && Mathf.RoundToInt(candidate.rect.y) == expectedY);
        if (!sprite) { EditorUtility.DisplayDialog("Power-up", "No existe un sprite cortado en esa columna/fila.", "OK"); return; }
        Undo.RecordObject(data, "Cambiar icono de power-up"); data.icon = sprite; EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
    }
}
#endif
