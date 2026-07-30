using System;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Properties;

[CreateAssetMenu(fileName = "GameData", menuName = "demoDataBinding/Game_Data")]
public class GameData : ScriptableObject, INotifyBindablePropertyChanged
{
    //=================[INotifyBindablePropertyChanged]//
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    private void Notify(string property) =>
        propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(property));
    

    [Space]

    //=================[Coin Value]====================//
    [Header("Value")]
    [SerializeField] private int coinValue = 500000;

    [CreateProperty]
    public int CoinValue
    {
        get => coinValue;
        set
        {
            if (coinValue == value) return;
            coinValue = value;
            Notify(nameof(CoinValue));
            Notify(nameof(ManageCoinText));
            Notify(nameof(ManageShopCoinText));
        }
    }

    [CreateProperty] public string ManageCoinText     => FormatAbbreviated(coinValue);
    [CreateProperty] public string ManageShopCoinText => FormatGrouped(coinValue);

    [Space]

    //=================[Shop Plans]====================//
    [Header("Shop Plans")]
    [SerializeField] private ShopPlan plan1 = new();
    [SerializeField] private ShopPlan plan2 = new();
    [SerializeField] private ShopPlan plan3 = new();
    [SerializeField] private ShopPlan plan4 = new();
    [SerializeField] private ShopPlan plan5 = new();
    [SerializeField] private ShopPlan plan6 = new();

    [CreateProperty] public ShopPlan Plan1 => plan1;
    [CreateProperty] public ShopPlan Plan2 => plan2;
    [CreateProperty] public ShopPlan Plan3 => plan3;
    [CreateProperty] public ShopPlan Plan4 => plan4;
    [CreateProperty] public ShopPlan Plan5 => plan5;
    [CreateProperty] public ShopPlan Plan6 => plan6;

    //=================[Number Formatting]=============//
    private static readonly (long threshold, float divisor, string suffix)[] NumberScales =
    {
        (1_000_000_000L, 1_000_000_000f, "B"),
        (1_000_000L,     1_000_000f,     "M"),
        (1_000L,         1_000f,         "K"),
    };

    private string FormatAbbreviated(long value)
    {
        bool isNegative = value < 0;
        long absValue = Math.Abs(value);
        foreach (var (threshold, divisor, suffix) in NumberScales)
        {
            if (absValue >= threshold)
            {
                float result = absValue / divisor;
                string formatted = $"{result:0.#} {suffix}";
                return isNegative ? $"-{formatted}" : formatted;
            }
        }
        return value.ToString();
    }

    private const char GroupSeparator = '،';

    private string FormatGrouped(long value)
    {
        bool isNegative = value < 0;
        string digits = Math.Abs(value).ToString();
        var sb = new StringBuilder();
        int firstGroup = digits.Length % 3;
        if (firstGroup == 0) firstGroup = 3;
        sb.Append(digits, 0, firstGroup);
        for (int i = firstGroup; i < digits.Length; i += 3)
        {
            sb.Append(GroupSeparator);
            sb.Append(digits, i, 3);
        }
        return isNegative ? $"-{sb}" : sb.ToString();
    }
}