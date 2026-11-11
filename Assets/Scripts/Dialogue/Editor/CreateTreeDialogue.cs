#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CreateTreeDialogues
{
    [MenuItem("Tools/Cursed Forest/Create Starter Tree Dialogues")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Dialogue")) AssetDatabase.CreateFolder("Assets", "Dialogue");
        CreateAsset("01_Introduction", new[] {
            new TreeDialogueLine("Alderworn", "Slowly, little traveler. That was a heavy fall."),
            new TreeDialogueLine("Alien", "The ship... I remember the flight, but not landing."),
            new TreeDialogueLine("Alderworn", "You did not land gently. I lifted what I could."),
            new TreeDialogueLine("Alien", "Your root. It's bleeding."),
            new TreeDialogueLine("Alderworn", "It will mend. Can you stand?"),
            new TreeDialogueLine("Alien", "I think so. Where am I?"),
            new TreeDialogueLine("Alderworn", "Wyrmwood. My name is Alderworn."),
            new TreeDialogueLine("Alien", "I need to contact my crew. The transmitter is damaged."),
            new TreeDialogueLine("Alderworn", "Then rest beneath my branches. When you are ready, we will see what can be repaired.")
        });
        CreateAsset("02_AboutTheForest", new[] {
            new TreeDialogueLine("Alien", "Has this forest always been like this?"),
            new TreeDialogueLine("Alderworn", "Once, water ran beneath these roots. There were voices in every clearing."),
            new TreeDialogueLine("Alien", "What happened to them?"),
            new TreeDialogueLine("Alderworn", "Magrenta cursed this place. Those who could leave fled. The others learned to endure."),
            new TreeDialogueLine("Alien", "And you stayed?"),
            new TreeDialogueLine("Alderworn", "My roots run deep."),
            new TreeDialogueLine("Alien", "What is that black growth in the forest?"),
            new TreeDialogueLine("Alderworn", "Gloom. It gathers where the curse chokes living wood."),
            new TreeDialogueLine("Alien", "Can it be removed?"),
            new TreeDialogueLine("Alderworn", "Some of it. Bring what you recover to me. I can turn it into resin - perhaps enough to seal your transmitter."),
            new TreeDialogueLine("Alien", "You would help me leave?"),
            new TreeDialogueLine("Alderworn", "Yes. You have people waiting for you.")
        });
        CreateAsset("03_Reserved", new TreeDialogueLine[0]);
        AssetDatabase.SaveAssets();
        Debug.Log("Tree dialogue assets are ready in Assets/Dialogue. Existing assets were kept.");
    }
    private static void CreateAsset(string name, TreeDialogueLine[] lines)
    {
        string path = "Assets/Dialogue/" + name + ".asset";
        if (AssetDatabase.LoadMainAssetAtPath(path)) return;
        var asset = ScriptableObject.CreateInstance<TreeDialogueData>();
        asset.lines = lines;
        AssetDatabase.CreateAsset(asset, path);
    }
}
#endif
