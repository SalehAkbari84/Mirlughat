using UnityEngine;
using UnityEngine.UIElements;


[
 DefaultExecutionOrder(-1000), 
 RequireComponent(typeof(PanelRenderer)), 
 RequireComponent(typeof(ManageUISlider)),
 RequireComponent(typeof(ManageUIButton))
]
public class BaseReference : MonoBehaviour
{
    //=============[Inspector Variable]=================//
    [SerializeField] private PanelRenderer panelRenderer;
    [SerializeField] private GameData gameData;
    [SerializeField] private ManageUISlider manageUISlider;
    [SerializeField] private ManageUIButton manageUIButton;

    
    //=============[Public Variable]=================//
    public static PanelRenderer _panelRenderer;
    public static GameData _gameData;
    public static ManageUISlider _manageUISlider;
    public static ManageUIButton _manageUIButton;

    private void Awake()
    {
        if (panelRenderer != null &&  
            gameData != null && 
            manageUISlider != null && 
            manageUIButton != null)
        {
            _panelRenderer = panelRenderer;
            _gameData = gameData;
            _manageUISlider = manageUISlider;
            _manageUIButton = manageUIButton;
        }
        else
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            _gameData = GetComponent<GameData>();
            _manageUISlider = GetComponent<ManageUISlider>();
            _manageUIButton = GetComponent<ManageUIButton>();
            
            if (_panelRenderer == null)
            {
                Debug.LogError("PanelRenderer not found! Please assign it in Inspector or add to GameObject.");
            }
            else if(_gameData == null)
            {
                Debug.LogError("GameData not found! Please assign it in Inspector or add to GameObject.");
            }
            else if(_manageUISlider == null)
            {
                Debug.LogError("ManageUISlider not found! Please assign it in Inspector or add to GameObject.");
            }
            else if(_manageUIButton == null)
            {
                Debug.LogError("ManageUIButton not found! Please assign it in Inspector or add to GameObject.");
            }
        }
    }
}