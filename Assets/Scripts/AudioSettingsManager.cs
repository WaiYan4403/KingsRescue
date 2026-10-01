using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsManager : MonoBehaviour
{
    [SerializeField] private float masterVolume = 1.0f; // Default volume level (0.0 to 1.0)
    
    // The exact path or name of the VCA as written in FMOD Studio
    private const string MasterVcaPath = "vca:/Master"; 
    private FMOD.Studio.VCA masterVca;

    void Start()
    {
        // 1. Fetch the reference from the FMOD Runtime Manager
        //masterVca = FMODUnity.RuntimeManager.GetVCA(MasterVcaPath);

        // 2. Initialize the UI slider matching FMOD (Ranges from 0.0 to 1.0)
        //masterVca.getVolume(out float currentVolume);
        //masterVolume = currentVolume;


        masterVca.setVolume(masterVolume);
    }

    public void SetMasterVolume(float volume)
    {
        // 4. Update the volume level dynamically
        masterVca.setVolume(volume);
    }
}
