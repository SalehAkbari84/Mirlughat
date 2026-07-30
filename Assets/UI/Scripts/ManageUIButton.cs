using System.Collections.Generic;
using UIToolkit.Animation.Timeline;
using UnityEngine;
using UnityEngine.UIElements;

public class ManageUIButton : MonoBehaviour
{
    //==============[Private Variable]==============//
    [Header("Animation Section")] 
    
    [SerializeField] private UISceneAnimation openSettingPanel;
    [SerializeField] private UISceneAnimation closeSettingPanel;
    [SerializeField] private UISceneAnimation openAdPanel;
    [SerializeField] private UISceneAnimation closeAdPanel;
    [SerializeField] private UISceneAnimation openStorePanel;
    [SerializeField] private UISceneAnimation closeStorePanel;
    
 
    //================[UI Callbacks]===============//
    public void OpenSettingPanel(ClickEvent evt)
    {
        UIPanel.WhenReady(gameObject, root =>
        {
            UISequenceRunner.Play(openSettingPanel, root);
        });
    }

    public void CloseSettingPanel(ClickEvent evt)
    {
        UIPanel.WhenReady(gameObject, root =>
        {
            UISequenceRunner.Play(closeSettingPanel, root);
        });
    }
    
    public void OpenAdPanel(ClickEvent evt)
    {
        UIPanel.WhenReady(gameObject, root =>
        {
            UISequenceRunner.Play(openAdPanel, root);
        });
    }
    
    public void CloseAdPanel(ClickEvent evt)
    {
        UIPanel.WhenReady(gameObject, root =>
        {
            UISequenceRunner.Play(closeAdPanel, root);
        });
    }
    
    public void OpenStorePanel(ClickEvent evt)
    {
        UIPanel.WhenReady(gameObject, root =>
        {
            UISequenceRunner.Play(openStorePanel, root);
        });
    }
    
    public void CloseStorePanel(ClickEvent evt)
    {
        UIPanel.WhenReady(gameObject, root =>
        {
            UISequenceRunner.Play(closeStorePanel, root);
        });
    }
}