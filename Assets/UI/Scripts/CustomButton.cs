using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// CustomButton — از UIAnimationOptimizer برای انیمیشن استفاده می‌کنه
/// به جای USS transition که در Build لگ داره.
/// </summary>
[RequireComponent(typeof(UIAnimationOptimizer))]
public class CustomButton : MonoBehaviour
{
    //=============[Buttons]===========================//
    private Button music_status_btn;
    private Button soundeffect_status_btn;
    private Button vibrator_status_btn;

    //=============[Handles]==========================//
    private Image music_handle_ico;
    private Image soundeffect_handle_ico;
    private Image vibrator_handle_ico;

    //=============[State]============================//
    private bool music_status      = true;
    private bool soundeffect_status = true;
    private bool vibrator_status   = true;

    //=============[Dependencies]=====================//
    private UIAnimationOptimizer _animator;

    //=============[Lifecycle]========================//
    private void Awake()
    {
        _animator = GetComponent<UIAnimationOptimizer>();
    }

    private void OnEnable()
    {
        if (BaseReference._panelRenderer != null)
            BaseReference._panelRenderer.RegisterUIReloadCallback(OnUIReload);
        else
            Debug.LogWarning("[CustomButton] _panelRenderer is null in OnEnable.");
    }

    private void OnDisable()
    {
        if (BaseReference._panelRenderer != null)
            BaseReference._panelRenderer.UnregisterUIReloadCallback(OnUIReload);

        UnsubscribeAll();
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement rootElement, int version)
    {
        UnsubscribeAll();
        FindButtonReferences(rootElement);
        FindImageReferences(rootElement);
        SubscribeAll();
    }

    //=============[Subscribe / Unsubscribe]==========//
    private void SubscribeAll()
    {
        if (music_status_btn       != null) music_status_btn.clicked       += MusicButtonClicked;
        if (soundeffect_status_btn != null) soundeffect_status_btn.clicked += SoundEffectButtonClicked;
        if (vibrator_status_btn    != null) vibrator_status_btn.clicked    += VibratorButtonClicked;
    }

    private void UnsubscribeAll()
    {
        if (music_status_btn       != null) music_status_btn.clicked       -= MusicButtonClicked;
        if (soundeffect_status_btn != null) soundeffect_status_btn.clicked -= SoundEffectButtonClicked;
        if (vibrator_status_btn    != null) vibrator_status_btn.clicked    -= VibratorButtonClicked;
    }

    //=============[Click Handlers]===================//
    private void MusicButtonClicked()
    {
        music_status = !music_status;
        _animator.AnimateToggle(music_status_btn, music_handle_ico, music_status);
    }

    private void SoundEffectButtonClicked()
    {
        soundeffect_status = !soundeffect_status;
        _animator.AnimateToggle(soundeffect_status_btn, soundeffect_handle_ico, soundeffect_status);
    }

    private void VibratorButtonClicked()
    {
        vibrator_status = !vibrator_status;
        _animator.AnimateToggle(vibrator_status_btn, vibrator_handle_ico, vibrator_status);
    }

    //=============[Find References]==================//
    private void FindButtonReferences(VisualElement rootElement)
    {
        music_status_btn       = rootElement.Q<Button>("music-status-btn");
        soundeffect_status_btn = rootElement.Q<Button>("soundeffect-status-btn");
        vibrator_status_btn    = rootElement.Q<Button>("vibrator-status-btn");

        if (music_status_btn       == null) Debug.LogWarning("[CustomButton] 'music-status-btn' not found.");
        if (soundeffect_status_btn == null) Debug.LogWarning("[CustomButton] 'soundeffect-status-btn' not found.");
        if (vibrator_status_btn    == null) Debug.LogWarning("[CustomButton] 'vibrator-status-btn' not found.");
    }

    private void FindImageReferences(VisualElement rootElement)
    {
        music_handle_ico       = rootElement.Q<Image>("handle-ico");
        soundeffect_handle_ico = rootElement.Q<Image>("soundeffect-handle-ico");
        vibrator_handle_ico    = rootElement.Q<Image>("vibrator-handle-ico");

        if (music_handle_ico       == null) Debug.LogWarning("[CustomButton] 'handle-ico' not found.");
        if (soundeffect_handle_ico == null) Debug.LogWarning("[CustomButton] 'soundeffect-handle-ico' not found.");
        if (vibrator_handle_ico    == null) Debug.LogWarning("[CustomButton] 'vibrator-handle-ico' not found.");
    }
}
