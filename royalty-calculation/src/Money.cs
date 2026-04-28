using System.Globalization;

namespace AspirePoc.RoyaltyCalculation;

public readonly record struct Money(decimal Amount, string Currency)
{
    public Money ApplyDiscount(decimal rate) =>
        this with { Amount = Math.Round(Amount * (1 - rate), 2, MidpointRounding.ToEven) };

    public override string ToString() =>
        $"{Amount.ToString("F2", CultureInfo.InvariantCulture)} {Currency}";
}
