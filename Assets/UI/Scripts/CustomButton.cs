using UnityEngine;
using UnityEngine.UIElements;


public class CustomButton : MonoBehaviour
{
    //=============[Public Variable Button]============//
    private Button music_status_btn;
    private Button soundeffect_status_btn;
    private Button vibrator_status_btn;
    
    //=============[Public Variable Image]============//
    private Image music_handle_ico;
    private Image soundeffect_handle_ico;
    private Image vibrator_handle_ico;

    //=============[Button Status]====================//
    private bool music_status = true;
    private bool soundeffect_status = true;
    private bool vibrator_status = true;
    
    private void OnEnable()
    {
        if (BaseReference._panelRenderer != null)
        {
            BaseReference._panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }
        else
        {
            Debug.LogWarning("[BaseUIElementReference] _panelRenderer is null in OnEnable. " +
                             "Check script execution order (BaseReference must run first).");
        }
    }
    
    private void OnDisable()
    {
        if (BaseReference._panelRenderer != null)
        {
            BaseReference._panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }
    }
    
    private void OnUIReload(PanelRenderer renderer, VisualElement rootElement, int version)
    {
        FindButtonReferences(rootElement);
        FindImageReferences(rootElement);

        if (music_status_btn != null)
        {
            music_status_btn.clicked += MusicButtonClicked;
        }

        if (soundeffect_status_btn != null)
        {
            soundeffect_status_btn.clicked += SoundEffectButtonClicked;
        }

        if (vibrator_status_btn != null)
        {
            vibrator_status_btn.clicked += VibratorButtonClicked;
        }
    }

    private void MusicButtonClicked()
    {
        music_status = !music_status;
        ApplyToggleVisual(music_status, music_handle_ico, music_status_btn);
    }

    private void SoundEffectButtonClicked()
    {
        soundeffect_status = !soundeffect_status;
        ApplyToggleVisual(soundeffect_status, soundeffect_handle_ico, soundeffect_status_btn);
    }

    private void VibratorButtonClicked()
    {
        vibrator_status = !vibrator_status;
        ApplyToggleVisual(vibrator_status, vibrator_handle_ico, vibrator_status_btn);
    }

    private void ApplyToggleVisual(bool status, Image handle, Button button)
    {
        if (handle == null || button == null)
            return;

        if (status)
        {
            ColorUtility.TryParseHtmlString("#22C55E", out var green);

            handle.style.translate = new Translate(new Length(124, LengthUnit.Percent), 0, 0);
            button.style.backgroundColor = green;
        }
        else
        {
            ColorUtility.TryParseHtmlString("#EF4444", out var red);

            handle.style.translate = new Translate(new Length(0, LengthUnit.Percent), 0, 0);
            button.style.backgroundColor = red;
        }
    }

    //=============[Private Helper Functions]============//

    private void FindButtonReferences(VisualElement rootElement)
    {
        music_status_btn = rootElement.Q<Button>("music-status-btn");
        soundeffect_status_btn = rootElement.Q<Button>("soundeffect-status-btn");
        vibrator_status_btn = rootElement.Q<Button>("vibrator-status-btn");
        
        if (music_status_btn == null)
            Debug.LogWarning("[BaseUIElementReference] 'music_status_btn' not found in visual tree. Check the name in UXML.");
        if (soundeffect_status_btn == null)
            Debug.LogWarning("[BaseUIElementReference] 'soundeffect_status_btn' not found in visual tree. Check the name in UXML.");
        if (vibrator_status_btn == null)
            Debug.LogWarning("[BaseUIElementReference] 'vibrator_status_btn' not found in visual tree. Check the name in UXML.");

    }

    private void FindImageReferences(VisualElement rootElement)
    {
        music_handle_ico = rootElement.Q<Image>("handle-ico");
        soundeffect_handle_ico = rootElement.Q<Image>("soundeffect-handle-ico");
        vibrator_handle_ico = rootElement.Q<Image>("vibrator-handle-ico");

        if (music_handle_ico == null)
            Debug.LogWarning("[BaseUIElementReference] 'handle_ico' not found in visual tree. Check the name in UXML.");
        if (soundeffect_handle_ico == null)
            Debug.LogWarning("[BaseUIElementReference] 'soundeffect_handle_ico' not found in visual tree. Check the name in UXML.");
        if (vibrator_handle_ico == null)
            Debug.LogWarning("[BaseUIElementReference] 'vibrator_handle_ico' not found in visual tree. Check the name in UXML.");

    }
}