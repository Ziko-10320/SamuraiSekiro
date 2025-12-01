using UnityEngine;

[System.Serializable]
public class QTEKey
{
    public string keyName;      // A friendly name like "Space" or "Shift"
    public KeyCode keyCode;    // The actual keyboard key
    public Sprite keySprite;    // The UI image for this key
}
