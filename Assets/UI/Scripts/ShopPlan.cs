using System;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Properties;

[Serializable]
public class ShopPlan : INotifyBindablePropertyChanged
{
    //=================[Backing Fields]================//
    [SerializeField] private string title         = "مقدار خوب";
    [SerializeField] private Sprite coinIcon;
    [SerializeField] private int    coinAmount    = 6000;
    [SerializeField] private int    priceInTomans = 25000;

    //=================[INotifyBindablePropertyChanged]//
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    private void Notify(string property) =>
        propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(property));

    //=================[Bindable Properties]===========//
    [CreateProperty]
    public string Title
    {
        get => title;
        set { if (title == value) return; title = value; Notify(nameof(Title)); }
    }

    [CreateProperty]
    public Sprite CoinIcon
    {
        get => coinIcon;
        set { if (coinIcon == value) return; coinIcon = value; Notify(nameof(CoinIcon)); }
    }

    [CreateProperty]
    public int CoinAmount
    {
        get => coinAmount;
        set
        {
            if (coinAmount == value) return;
            coinAmount = value;
            Notify(nameof(CoinAmount));
            Notify(nameof(CoinAmountText));
        }
    }

    [CreateProperty]
    public int PriceInTomans
    {
        get => priceInTomans;
        set
        {
            if (priceInTomans == value) return;
            priceInTomans = value;
            Notify(nameof(PriceInTomans));
            Notify(nameof(PriceText));
        }
    }

    //=================[Formatted Text]================//
    [CreateProperty] public string CoinAmountText => FormatGrouped(coinAmount);
    [CreateProperty] public string PriceText      => $"{FormatGrouped(priceInTomans)} تومان";

    //=================[Formatter]=====================//
    private const char Sep = '،';

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
            sb.Append(Sep);
            sb.Append(digits, i, 3);
        }
        return isNegative ? $"-{sb}" : sb.ToString();
    }
}