using UnityEngine;
using UnityEngine.UIElements;

public class ManageUISlider : MonoBehaviour
{
    //=============[UI Callbacks]================//
    
    public void OnSoundEffectSliderChanged(ChangeEvent<float> evt)
    {
        //BaseReference._gameData.soundEffectSlider = evt.newValue;
    }
    
    public void OnMusicSliderChanged(ChangeEvent<float> evt)
    {
        //BaseReference._gameData.musicSlider = evt.newValue;
    }
}