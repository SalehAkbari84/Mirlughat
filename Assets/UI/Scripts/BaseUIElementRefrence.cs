using UnityEngine;
using UnityEngine.UIElements;

public class BaseUIElementRefrence : MonoBehaviour
{
    //=============[Buttons]===========================//
    private Button open_Setting_Btn;
    private Button close_Setting_Btn;
    private Button open_Ad_Btn;
    private Button close_Ad_Btn;
    private Button open_Store_Btn;
    private Button close_Store_Btn;
    private Button add_Coin_Btn;

    //=============[Lifecycle]=========================//
    private void OnEnable()
    {
        if (BaseReference._panelRenderer != null)
            BaseReference._panelRenderer.RegisterUIReloadCallback(OnUIReload);
        else
            Debug.LogWarning("[BaseUIElementReference] _panelRenderer is null in OnEnable.");
    }

    private void OnDisable()
    {
        if (BaseReference._panelRenderer != null)
            BaseReference._panelRenderer.UnregisterUIReloadCallback(OnUIReload);

        UnsubscribeAll(); // وقتی غیرفعال می‌شه همه event ها پاک بشن
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement rootElement, int version)
    {
        UnsubscribeAll();       // اول همه callback های قبلی رو پاک کن
        FindButtonReferences(rootElement);
        SubscribeAll();         // بعد دوباره subscribe کن
    }

    //=============[Subscribe / Unsubscribe]===========//
    private void SubscribeAll()
    {
        open_Setting_Btn?.RegisterCallback<ClickEvent>(BaseReference._manageUIButton.OpenSettingPanel);
        close_Setting_Btn?.RegisterCallback<ClickEvent>(BaseReference._manageUIButton.CloseSettingPanel);
        open_Ad_Btn?.RegisterCallback<ClickEvent>(BaseReference._manageUIButton.OpenAdPanel);
        add_Coin_Btn?.RegisterCallback<ClickEvent>(BaseReference._manageUIButton.OpenAdPanel);
        close_Ad_Btn?.RegisterCallback<ClickEvent>(BaseReference._manageUIButton.CloseAdPanel);
        open_Store_Btn?.RegisterCallback<ClickEvent>(BaseReference._manageUIButton.OpenStorePanel);
        close_Store_Btn?.RegisterCallback<ClickEvent>(BaseReference._manageUIButton.CloseStorePanel);
    }

    private void UnsubscribeAll()
    {
        open_Setting_Btn?.UnregisterCallback<ClickEvent>(BaseReference._manageUIButton.OpenSettingPanel);
        close_Setting_Btn?.UnregisterCallback<ClickEvent>(BaseReference._manageUIButton.CloseSettingPanel);
        open_Ad_Btn?.UnregisterCallback<ClickEvent>(BaseReference._manageUIButton.OpenAdPanel);
        add_Coin_Btn?.UnregisterCallback<ClickEvent>(BaseReference._manageUIButton.OpenAdPanel);
        close_Ad_Btn?.UnregisterCallback<ClickEvent>(BaseReference._manageUIButton.CloseAdPanel);
        open_Store_Btn?.UnregisterCallback<ClickEvent>(BaseReference._manageUIButton.OpenStorePanel);
        close_Store_Btn?.UnregisterCallback<ClickEvent>(BaseReference._manageUIButton.CloseStorePanel);
    }

    //=============[Find References]===================//
    private void FindButtonReferences(VisualElement rootElement)
    {
        open_Setting_Btn  = rootElement.Q<Button>("open-setting-btn");
        close_Setting_Btn = rootElement.Q<Button>("close-setting-btn");
        open_Ad_Btn       = rootElement.Q<Button>("open-ad-btn");
        close_Ad_Btn      = rootElement.Q<Button>("cancel-ad");
        open_Store_Btn    = rootElement.Q<Button>("shop-btn");
        close_Store_Btn   = rootElement.Q<Button>("shop-close-btn");
        add_Coin_Btn      = rootElement.Q<Button>("add-coin-btn");

        if (open_Setting_Btn  == null) Debug.LogWarning("[BaseUIElementReference] 'open-setting-btn' not found.");
        if (close_Setting_Btn == null) Debug.LogWarning("[BaseUIElementReference] 'close-setting-btn' not found.");
        if (open_Ad_Btn       == null) Debug.LogWarning("[BaseUIElementReference] 'open-ad-btn' not found.");
        if (close_Ad_Btn      == null) Debug.LogWarning("[BaseUIElementReference] 'cancel-ad' not found.");
        if (open_Store_Btn    == null) Debug.LogWarning("[BaseUIElementReference] 'shop-btn' not found.");
        if (close_Store_Btn   == null) Debug.LogWarning("[BaseUIElementReference] 'shop-close-btn' not found.");
        if (add_Coin_Btn      == null) Debug.LogWarning("[BaseUIElementReference] 'add-coin-btn' not found.");
    }
}