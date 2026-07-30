using UnityEngine;
using UnityEngine.UIElements;

public class BaseUIElementRefrence : MonoBehaviour
{
    //=============[Public Variable Slider]============//

    
    //=============[Public Variable Button]============//
    private Button open_Setting_Btn;
    private Button close_Setting_Btn;
    private Button open_Ad_Btn;
    private Button close_Ad_Btn;
    private Button open_Store_Btn;
    private Button close_Store_Btn;
    private Button add_Coin_Btn;


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
        //FindSliderReferences(rootElement);
        FindButtonReferences(rootElement);

        //SetSliderValue();
        OnClickButton();
    }

    //=============[Private Helper Functions]============//
    private void FindSliderReferences(VisualElement rootElement)
    {

    }

    private void FindButtonReferences(VisualElement rootElement)
    {
        open_Setting_Btn = rootElement.Q<Button>("open-setting-btn");
        close_Setting_Btn = rootElement.Q<Button>("close-setting-btn");
        open_Ad_Btn = rootElement.Q<Button>("open-ad-btn");
        close_Ad_Btn = rootElement.Q<Button>("cancel-ad");
        open_Store_Btn = rootElement.Q<Button>("shop-btn");
        close_Store_Btn = rootElement.Q<Button>("shop-close-btn");
        add_Coin_Btn = rootElement.Q<Button>("add-coin-btn");
        
        
        if (open_Setting_Btn == null)
            Debug.LogWarning("[BaseUIElementReference] 'open_Setting_Btn' not found in visual tree. Check the name in UXML.");
        if (close_Setting_Btn == null)
            Debug.LogWarning("[BaseUIElementReference] 'close_Setting_Btn' not found in visual tree. Check the name in UXML.");
        if (open_Ad_Btn == null)
            Debug.LogWarning("[BaseUIElementReference] 'open_Ad_Btn' not found in visual tree. Check the name in UXML.");
        if (close_Ad_Btn == null)
            Debug.LogWarning("[BaseUIElementReference] 'close_Ad_Btn' not found in visual tree. Check the name in UXML.");
        if (open_Store_Btn == null)
            Debug.LogWarning("[BaseUIElementReference] 'open_Store_Btn' not found in visual tree. Check the name in UXML.");
        if (close_Store_Btn == null)
            Debug.LogWarning("[BaseUIElementReference] 'close_Store_Btn' not found in visual tree. Check the name in UXML.");
        if (add_Coin_Btn == null)
            Debug.LogWarning("[BaseUIElementReference] 'add_Coin_Btn' not found in visual tree. Check the name in UXML.");

    }
    
    //=============[Manage Slider]============//
    private void SetSliderValue()
    {
        //soundEffectSlider.RegisterCallback<ChangeEvent<float>>(BaseReference._manageUISlider.OnSoundEffectSliderChanged);
        //musicSlider.RegisterCallback<ChangeEvent<float>>(BaseReference._manageUISlider.OnMusicSliderChanged);
    }

    //=============[Manage Button]============//
    private void OnClickButton()
    {
        open_Setting_Btn.RegisterCallback<ClickEvent>(BaseReference._manageUIButton.OpenSettingPanel);
        close_Setting_Btn.RegisterCallback<ClickEvent>(BaseReference._manageUIButton.CloseSettingPanel);
        open_Ad_Btn.RegisterCallback<ClickEvent>(BaseReference._manageUIButton.OpenAdPanel);
        add_Coin_Btn.RegisterCallback<ClickEvent>(BaseReference._manageUIButton.OpenAdPanel);
        close_Ad_Btn.RegisterCallback<ClickEvent>(BaseReference._manageUIButton.CloseAdPanel);
        open_Store_Btn.RegisterCallback<ClickEvent>(BaseReference._manageUIButton.OpenStorePanel);
        close_Store_Btn.RegisterCallback<ClickEvent>(BaseReference._manageUIButton.CloseStorePanel);
    }
    
}