using UnityEngine;

[CreateAssetMenu(fileName = "New Voice Profile", menuName = "Dialog/Voice Profile")]
public class VoiceProfile : ScriptableObject
{
    [Header("Sounds A-Z (Element 0-25)")]
    // Element 26 คือเสียงเงียบ หรือเสียงสำหรับตัวอักษรพิเศษ
    public AudioClip[] alphabetSounds = new AudioClip[27];
}