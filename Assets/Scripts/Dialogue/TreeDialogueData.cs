using UnityEngine;

[System.Serializable]
public class TreeDialogueLine
{
    public string speaker;
    [TextArea(2, 6)] public string text;
    public TreeDialogueLine(string speaker, string text) { this.speaker = speaker; this.text = text; }
}

[CreateAssetMenu(menuName = "Cursed Forest/Tree Dialogue")]
public class TreeDialogueData : ScriptableObject
{
    public TreeDialogueLine[] lines = new TreeDialogueLine[0];
    public bool IsPlayable
    {
        get
        {
            if (lines == null || lines.Length == 0) return false;
            foreach (TreeDialogueLine line in lines)
                if (line == null || string.IsNullOrWhiteSpace(line.text)) return false;
            return true;
        }
    }
}
